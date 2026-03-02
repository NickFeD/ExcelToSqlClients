using ExcelToSqlClients.Core.Models;
using Microsoft.Data.SqlClient;

namespace ExcelToSqlClients.Dynamic.Import.Console;

/// <summary>
/// Сервис импорта, по стилю как ClientImportService в "оригинальном" проекте.
/// Управляет батчами, апсертом и политикой ошибок (fail-fast vs ignore).
/// </summary>
public sealed class DynamicImportService
{
    private readonly IDynamicRowReader _reader;
    private readonly DynamicRepository _repo;

    public DynamicImportService(IDynamicRowReader reader, DynamicRepository repo)
    {
        _reader = reader;
        _repo = repo;
    }

    public async Task<ImportResult> ImportAsync(
        string cs,
        DynamicSchemaConfig schema,
        string excelPath,
        int batchSize,
        bool ignoreErrors,
        CancellationToken ct)
    {
        if (batchSize <= 0)
            batchSize = 1000;

        var result = new ImportResult();

        var keyCol = schema.Columns.FirstOrDefault(c => c.IsPrimaryKey)?.Name
                  ?? schema.Columns.FirstOrDefault(c => c.IsUnique)?.Name
                  ?? throw new InvalidOperationException("No key column. Mark one column IsPrimaryKey=true (or IsUnique=true)." );

        var allCols = schema.Columns.Select(c => c.Name).ToList();
        var updateCols = allCols.Where(c => !c.Equals(keyCol, StringComparison.OrdinalIgnoreCase)).ToList();

        var insertSql = BuildInsertSql(schema.Schema, schema.Table, allCols);
        var updateSql = BuildUpdateSql(schema.Schema, schema.Table, keyCol, updateCols);

        await using var con = new SqlConnection(cs);
        await con.OpenAsync(ct);

        var batch = new List<DynamicRowReadResult>(batchSize);

        foreach (var read in _reader.Read(excelPath, schema))
        {
            ct.ThrowIfCancellationRequested();
            result.ReadTotal++;

            if (!read.IsSuccess)
            {
                result.Skipped++;
                if (read.Error != null) result.Errors.Add(read.Error);

                if (!ignoreErrors)
                    throw new InvalidOperationException($"Row {read.RowNumber} ({read.Error?.Field}): {read.Error?.Message}. Raw='{read.Error?.RawValue}'");

                continue;
            }

            // если Params есть, но Error тоже есть - считаем как skipped (как в оригинале)
            if (read.Error != null)
            {
                result.Skipped++;
                result.Errors.Add(read.Error);

                if (!ignoreErrors)
                    throw new InvalidOperationException($"Row {read.RowNumber} ({read.Error.Field}): {read.Error.Message}. Raw='{read.Error.RawValue}'");

                continue;
            }

            batch.Add(read);

            if (batch.Count >= batchSize)
            {
                await FlushBatch(con, schema, keyCol, insertSql, updateSql, batch, result, ignoreErrors, ct);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await FlushBatch(con, schema, keyCol, insertSql, updateSql, batch, result, ignoreErrors, ct);
            batch.Clear();
        }

        return result;
    }

    private async Task FlushBatch(
        SqlConnection con,
        DynamicSchemaConfig schema,
        string keyCol,
        string insertSql,
        string updateSql,
        List<DynamicRowReadResult> batch,
        ImportResult result,
        bool ignoreErrors,
        CancellationToken ct)
    {
        try
        {
            var (inserted, updated) = await _repo.UpsertBatchAsync(con, schema, keyCol, insertSql, updateSql, batch, ct);
            result.Inserted += inserted;
            result.Updated += updated;
        }
        catch (Exception ex)
        {
            if (!ignoreErrors)
                throw;

            // ignoreErrors=true: просто логируем и считаем как skipped
            result.Skipped += batch.Count;
            result.Errors.Add(new ImportError { RowNumber = 0, Field = "*", RawValue = "", Message = $"Batch failed -> skipped: {ex.Message}" });
        }
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

    private static string EscId(string s) => (s ?? "").Replace("]", "]]" );
}
