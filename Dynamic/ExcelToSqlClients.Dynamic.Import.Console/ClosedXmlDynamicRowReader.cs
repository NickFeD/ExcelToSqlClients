using ClosedXML.Excel;
using Dapper;
using ExcelToSqlClients.Core.Models;

namespace ExcelToSqlClients.Dynamic.Import.Console;

/// <summary>
/// Reader, по стилю как ClosedXmlClientReader в "оригинальном" проекте.
/// Только читает Excel и мапит в DynamicParameters + ошибки.
/// </summary>
public sealed class ClosedXmlDynamicRowReader : IDynamicRowReader
{
    public IEnumerable<DynamicRowReadResult> Read(string path, DynamicSchemaConfig schema)
    {
        var keyCol = schema.Columns.FirstOrDefault(c => c.IsPrimaryKey)?.Name
                  ?? schema.Columns.FirstOrDefault(c => c.IsUnique)?.Name;

        if (string.IsNullOrWhiteSpace(keyCol))
            throw new InvalidOperationException("No key column. Mark one column IsPrimaryKey=true (or IsUnique=true).");

        var typeByColumn = schema.Columns.ToDictionary(c => c.Name, c => c.SqlType, StringComparer.OrdinalIgnoreCase);
        var nullableByColumn = schema.Columns.ToDictionary(c => c.Name, c => c.Nullable, StringComparer.OrdinalIgnoreCase);
        var allCols = schema.Columns.Select(c => c.Name).ToList();

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet(1);

        var map = ExcelHeaderMap.FromHeaderRow(ws.Row(1));
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        // строгая проверка наличия всех колонок из конфига в Excel (как "обязательные" заголовки)
        var missing = allCols
            .Where(c => !map.TryGetColumn(c, out _))
            .ToList();

        if (missing.Count > 0)
        {
            // это уже конфигурационная ошибка файла, нет смысла продолжать
            throw new InvalidOperationException("Missing Excel columns: " + string.Join(", ", missing));
        }

        for (int r = 2; r <= lastRow; r++)
        {
            var row = ws.Row(r);
            var errors = new List<ImportError>();

            object? rawKey = ReadCellByHeader(map, row, keyCol);
            var keyVal = DynamicValueConverter.ConvertToSqlType(keyCol, typeByColumn[keyCol], rawKey, r, errors, false);

            if (keyVal == null)
            {
                // если конвертер уже добавил причину - используем её, иначе даём общую
                if (errors.Count == 0)
                {
                    errors.Add(new ImportError
                    {
                        RowNumber = r,
                        Field = keyCol,
                        RawValue = rawKey?.ToString() ?? "",
                        Message = "Key missing/invalid"
                    });
                }

                yield return new DynamicRowReadResult { RowNumber = r, Error = errors[^1] };
                continue;
            }

            var p = new DynamicParameters();
            p.Add(keyCol, keyVal);

            bool hardFail = false;

            foreach (var col in allCols)
            {
                if (col.Equals(keyCol, StringComparison.OrdinalIgnoreCase))
                    continue;

                object? raw = ReadCellByHeader(map, row, col);
                var converted = DynamicValueConverter.ConvertToSqlType(col, typeByColumn[col], raw, r, errors, nullableByColumn[col]);

                if (!nullableByColumn[col] && converted == null)
                {
                    hardFail = true;

                    // если конвертер не добавил ошибку, добавим понятную для NOT NULL
                    if (errors.Count == 0 || !errors[^1].Field.Equals(col, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(new ImportError
                        {
                            RowNumber = r,
                            Field = col,
                            RawValue = raw?.ToString() ?? "",
                            Message = "NOT NULL: invalid or empty"
                        });
                    }

                    break;
                }

                p.Add(col, converted);
            }

            if (hardFail)
            {
                yield return new DynamicRowReadResult { RowNumber = r, Error = errors[^1] };
                continue;
            }

            if (errors.Count > 0)
            {
                // как в оригинале: можем вернуть и объект, и ошибку (сервис решит как считать)
                yield return new DynamicRowReadResult
                {
                    RowNumber = r,
                    Key = keyVal.ToString() ?? "",
                    Params = p,
                    Error = errors[0]
                };
                continue;
            }

            yield return new DynamicRowReadResult
            {
                RowNumber = r,
                Key = keyVal.ToString() ?? "",
                Params = p
            };
        }
    }

    private static object? ReadCellByHeader(ExcelHeaderMap map, IXLRow row, string header)
    {
        if (!map.TryGetColumn(header, out var col))
            return null;

        var cell = row.Cell(col);
        if (cell.IsEmpty())
            return null;

        return cell.DataType switch
        {
            XLDataType.Text => Normalize(cell.GetString()),
            XLDataType.DateTime => cell.GetDateTime().Date,
            XLDataType.Boolean => cell.GetBoolean(),
            XLDataType.Number => cell.GetDouble(),
            _ => Normalize(cell.GetString())
        };
    }

    private static string Normalize(string s) => (s ?? "").Trim().Replace("\u00A0", " ");
}
