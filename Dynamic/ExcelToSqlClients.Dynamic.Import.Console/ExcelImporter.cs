using ClosedXML.Excel;
using Dapper;
using ExcelToSqlClients.Core.Models;
using Microsoft.Data.SqlClient;

namespace ExcelToSqlClients.Dynamic.Import.Console;

public class ExcelImporter
{
    public async Task<ImportResult> ImportAsync(
        string cs,
        DynamicSchemaConfig schema,
        string excelPath,
        int batchSize,
        CancellationToken ct)
    {
        var result = new ImportResult();
        batchSize = batchSize <= 0 ? 1000 : batchSize;

        var keyCol = schema.Columns.FirstOrDefault(c => c.IsPrimaryKey)?.Name
                  ?? schema.Columns.FirstOrDefault(c => c.IsUnique)?.Name;

        if (string.IsNullOrWhiteSpace(keyCol))
            throw new InvalidOperationException("No key column. Mark one column IsPrimaryKey=true (or IsUnique=true).");

        var typeByColumn = schema.Columns.ToDictionary(c => c.Name, c => c.SqlType, StringComparer.OrdinalIgnoreCase);
        var nullableByColumn = schema.Columns.ToDictionary(c => c.Name, c => c.Nullable, StringComparer.OrdinalIgnoreCase);

        var allCols = schema.Columns.Select(c => c.Name).ToList();
        var updateCols = allCols.Where(c => !c.Equals(keyCol, StringComparison.OrdinalIgnoreCase)).ToList();

        var insertSql = BuildInsertSql(schema.Schema, schema.Table, allCols);
        var updateSql = BuildUpdateSql(schema.Schema, schema.Table, keyCol, updateCols);

        using var wb = new XLWorkbook(excelPath);
        var ws = wb.Worksheet(1);

        var headerIndex = BuildExcelHeaderIndex(ws.Row(1));
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        await using var con = new SqlConnection(cs);
        await con.OpenAsync(ct);

        var batch = new List<RowItem>(batchSize);

        for (int r = 2; r <= lastRow; r++)
        {
            ct.ThrowIfCancellationRequested();
            result.ReadTotal++;

            var row = ws.Row(r);
            var rowErrors = new List<ImportError>();

            object? rawKey = ReadCellByHeader(row, headerIndex, keyCol);
            var keyVal = DynamicValueConverter.ConvertToSqlType(keyCol, typeByColumn[keyCol], rawKey, r, rowErrors, false);

            if (keyVal == null)
            {
                result.Skipped++;
                rowErrors.Add(new ImportError { RowNumber = r, Field = keyCol, RawValue = rawKey?.ToString() ?? "", Message = "Key missing/invalid -> row skipped" });
                result.Errors.AddRange(rowErrors);
                continue;
            }

            var p = new DynamicParameters();
            p.Add(keyCol, keyVal);

            bool hardFail = false;

            foreach (var col in allCols)
            {
                if (col.Equals(keyCol, StringComparison.OrdinalIgnoreCase)) continue;

                object? raw = ReadCellByHeader(row, headerIndex, col);
                var converted = DynamicValueConverter.ConvertToSqlType(col, typeByColumn[col], raw, r, rowErrors, nullableByColumn[col]);

                if (!nullableByColumn[col] && converted == null)
                {
                    hardFail = true;
                    rowErrors.Add(new ImportError { RowNumber = r, Field = col, RawValue = raw?.ToString() ?? "", Message = "NOT NULL invalid -> row skipped" });
                    break;
                }

                p.Add(col, converted);
            }

            if (hardFail)
            {
                result.Skipped++;
                result.Errors.AddRange(rowErrors);
                continue;
            }

            if (rowErrors.Count > 0)
                result.Errors.AddRange(rowErrors);

            batch.Add(new RowItem(r, keyVal.ToString() ?? "", p));

            if (batch.Count >= batchSize)
            {
                await UpsertBatch(con, schema, keyCol, insertSql, updateSql, batch, result, ct);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await UpsertBatch(con, schema, keyCol, insertSql, updateSql, batch, result, ct);
            batch.Clear();
        }

        return result;
    }

    private static async Task UpsertBatch(
        SqlConnection con,
        DynamicSchemaConfig schema,
        string keyCol,
        string insertSql,
        string updateSql,
        List<RowItem> batch,
        ImportResult result,
        CancellationToken ct)
    {
        // last wins per key
        var byKey = new Dictionary<string, RowItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var it in batch) byKey[it.Key] = it;

        var keys = byKey.Keys.ToList();

        var existsSql = $@"SELECT [{EscId(keyCol)}] FROM [{EscId(schema.Schema)}].[{EscId(schema.Table)}]
WHERE [{EscId(keyCol)}] IN @Keys;";

        var existingKeys = (await con.QueryAsync<object>(
            new CommandDefinition(existsSql, new { Keys = keys }, cancellationToken: ct)))
            .Select(x => x.ToString() ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toInsert = new List<DynamicParameters>();
        var toUpdate = new List<DynamicParameters>();

        foreach (var kv in byKey)
        {
            if (existingKeys.Contains(kv.Key)) toUpdate.Add(kv.Value.Params);
            else toInsert.Add(kv.Value.Params);
        }

        using var tx = con.BeginTransaction();
        try
        {
            if (toInsert.Count > 0)
            {
                await con.ExecuteAsync(new CommandDefinition(insertSql, toInsert, tx, cancellationToken: ct));
                result.Inserted += toInsert.Count;
            }

            if (toUpdate.Count > 0)
            {
                await con.ExecuteAsync(new CommandDefinition(updateSql, toUpdate, tx, cancellationToken: ct));
                result.Updated += toUpdate.Count;
            }

            tx.Commit();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            result.Skipped += batch.Count;
            result.Errors.Add(new ImportError { RowNumber = 0, Field = "*", RawValue = "", Message = $"Batch failed -> skipped: {ex.Message}" });
        }
    }

    private static Dictionary<string, int> BuildExcelHeaderIndex(IXLRow headerRow)
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var h = Normalize(cell.GetString());
            if (string.IsNullOrWhiteSpace(h)) continue;
            if (!dict.ContainsKey(h)) dict[h] = cell.Address.ColumnNumber;
        }
        return dict;
    }

    private static object? ReadCellByHeader(IXLRow row, Dictionary<string, int> headerIndex, string header)
    {
        var h = Normalize(header);
        if (!headerIndex.TryGetValue(h, out var col)) return null;

        var cell = row.Cell(col);
        if (cell.IsEmpty()) return null;

        return cell.DataType switch
        {
            XLDataType.Text => Normalize(cell.GetString()),
            XLDataType.DateTime => cell.GetDateTime().Date,
            XLDataType.Boolean => cell.GetBoolean(),
            XLDataType.Number => cell.GetDouble(),
            _ => Normalize(cell.GetString())
        };
    }

    private static string BuildInsertSql(string schema, string table, List<string> cols)
    {
        var colsSql = string.Join(", ", cols.Select(c => $"[{EscId(c)}]"));
        var valsSql = string.Join(", ", cols.Select(c => $"@{c}"));
        return $@"INSERT INTO [{EscId(schema)}].[{EscId(table)}] ({colsSql}) VALUES ({valsSql});";
    }

    private static string BuildUpdateSql(string schema, string table, string keyCol, List<string> updateCols)
    {
        var setSql = string.Join(", ", updateCols.Select(c => $"[{EscId(c)}]=@{c}"));
        return $@"UPDATE [{EscId(schema)}].[{EscId(table)}]
SET {setSql}
WHERE [{EscId(keyCol)}]=@{keyCol};";
    }

    private static string Normalize(string s) => (s ?? "").Trim().Replace("\u00A0", " ");
    private static string EscId(string s) => (s ?? "").Replace("]", "]]");

    private readonly record struct RowItem(int RowNumber, string Key, DynamicParameters Params);
}