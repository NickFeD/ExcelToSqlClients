using ExcelToSqlClients.Core.Models;
using System.Globalization;

namespace ExcelToSqlClients.Dynamic.Import.Console;

public static class DynamicValueConverter
{
    public static object? ConvertToSqlType(
        string colName,
        string sqlType,
        object? raw,
        int rowNumber,
        List<ImportError> errors,
        bool nullable)
    {
        if (raw == null) return null;

        var baseType = NormalizeBase(sqlType);

        if (baseType is "nvarchar" or "varchar" or "nchar" or "char" or "text" or "ntext")
        {
            var s = raw.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        if (baseType is "date" or "datetime" or "datetime2" or "smalldatetime")
        {
            if (raw is DateTime dt) return dt.Date;

            var s = raw.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(s)) return null;

            var formats = new[] { "d.M.yyyy", "dd.MM.yyyy", "d.MM.yyyy", "dd.M.yyyy", "yyyy-MM-dd" };
            if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed.Date;

            // Если колонка nullable — считаем невалидное значение как NULL без ошибки.
            if (nullable) return null;

            errors.Add(new ImportError { RowNumber = rowNumber, Field = colName, RawValue = s, Message = "Invalid date -> NULL" });
            return null;
        }

        if (baseType is "tinyint")
        {
            if (raw is byte b) return b;
            if (byte.TryParse(raw.ToString()?.Trim(), out var v)) return v;

            if (nullable) return null;
            errors.Add(new ImportError { RowNumber = rowNumber, Field = colName, RawValue = raw.ToString() ?? "", Message = "Invalid tinyint -> NULL" });
            return null;
        }

        if (baseType is "int")
        {
            if (raw is int i) return i;
            if (raw is double d) return (int)Math.Round(d, 0);
            if (int.TryParse(raw.ToString()?.Trim(), out var v)) return v;

            if (nullable) return null;
            errors.Add(new ImportError { RowNumber = rowNumber, Field = colName, RawValue = raw.ToString() ?? "", Message = "Invalid int -> NULL" });
            return null;
        }

        if (baseType is "bigint")
        {
            if (raw is long l) return l;
            if (raw is int i) return (long)i;
            if (raw is double d) return (long)Math.Round(d, 0);
            if (long.TryParse(raw.ToString()?.Trim(), out var v)) return v;

            if (nullable) return null;
            errors.Add(new ImportError { RowNumber = rowNumber, Field = colName, RawValue = raw.ToString() ?? "", Message = "Invalid bigint -> NULL" });
            return null;
        }

        // fallback
        var fb = raw.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(fb) ? null : fb;
    }

    private static string NormalizeBase(string sqlType)
    {
        var t = (sqlType ?? "").Trim().ToLowerInvariant();
        var idx = t.IndexOf('(');
        if (idx > 0) t = t[..idx];
        return t;
    }
}
