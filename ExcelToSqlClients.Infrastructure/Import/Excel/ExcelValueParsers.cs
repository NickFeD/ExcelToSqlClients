using ClosedXML.Excel;
using ExcelToSqlClients.Core.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ExcelToSqlClients.Infrastructure.Import.Excel;

public static class ExcelValueParsers
{
    private static readonly Regex DigitsOnly = new(@"[^\d]+", RegexOptions.Compiled);
    private static readonly string[] DateFormats = ["d.M.yyyy", "dd.MM.yyyy", "d.MM.yyyy", "dd.M.yyyy"];

    public static string? ReadString(IXLCell cell)
    {
        var s = cell.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    public static string? ReadPhoneAsString(IXLCell cell)
    {
        // Телефон может лежать как число — GetString() вернет "7999..." или "7.999E+..."
        // Поэтому:
        // 1) пробуем взять как строку
        // 2) если пусто, но есть numeric value
        var raw = cell.GetString()?.Trim();

        if (!string.IsNullOrWhiteSpace(raw))
        {
            // оставим только цифры
            var digits = DigitsOnly.Replace(raw, "");
            return string.IsNullOrWhiteSpace(digits) ? null : digits;
        }

        if (cell.DataType == XLDataType.Number)
        {
            var d = cell.GetDouble();
            var asLong = (long)Math.Round(d, 0);
            return asLong <= 0 ? null : asLong.ToString(CultureInfo.InvariantCulture);
        }

        return null;
    }

    public static int? ReadIntNullable(IXLCell cell, int rowNumber, string field, List<ImportError> errors)
    {
        var s = ReadString(cell);
        if (s == null) return null;

        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val))
            return val;

        // иногда в Excel есть пробелы/запятые
        if (int.TryParse(s.Replace(" ", ""), NumberStyles.Integer, CultureInfo.GetCultureInfo("ru-RU"), out val))
            return val;

        errors.Add(new ImportError { RowNumber = rowNumber, Field = field, RawValue = s, Message = "Cannot parse int" });
        return null;
    }

    public static long? ReadLongNullable(IXLCell cell, int rowNumber, string field, List<ImportError> errors)
    {
        var s = ReadString(cell);
        if (s == null) return null;

        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val))
            return val;

        if (long.TryParse(s.Replace(" ", ""), NumberStyles.Integer, CultureInfo.GetCultureInfo("ru-RU"), out val))
            return val;

        errors.Add(new ImportError { RowNumber = rowNumber, Field = field, RawValue = s, Message = "Cannot parse long" });
        return null;
    }

    public static DateOnly? ReadDateOnlyNullable(IXLCell cell, int rowNumber, string field, List<ImportError> errors)
    {
        // 1) если Excel хранит дату
        if (cell.DataType == XLDataType.DateTime)
        {
            var dt = cell.GetDateTime();
            return DateOnly.FromDateTime(dt);
        }

        // 2) строковые даты типа "25.02.2026"
        var s = ReadString(cell);
        if (s == null) return null;

        if (DateOnly.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;

        // если в файле встречается "0.12.1952" пишем ошибку
        errors.Add(new ImportError { RowNumber = rowNumber, Field = field, RawValue = s, Message = "Cannot parse date" });
        return null;
    }

    public static byte? ReadGenderIdNullable(IXLCell cell, int rowNumber, string field, List<ImportError> errors)
    {
        var s = ReadString(cell);
        if (s == null) return null;

        var norm = s.Trim().ToLowerInvariant();

        if (norm is "м" or "муж" or "male" or "m" or "мужчина")
            return 1;

        if (norm is "ж" or "жен" or "female" or "f" or "женщина")
            return 2;

        // иногда может приходить "1"/"2"

        if (byte.TryParse(norm, out var b) && (b == 1 || b == 2))
            return b;

        errors.Add(new ImportError { RowNumber = rowNumber, Field = field, RawValue = s, Message = "Unknown gender value" });
        return null;
    }
}