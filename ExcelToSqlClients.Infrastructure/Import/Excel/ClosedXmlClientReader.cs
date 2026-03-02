using ClosedXML.Excel;
using ExcelToSqlClients.Core.Abstractions;
using ExcelToSqlClients.Core.Entities;
using ExcelToSqlClients.Core.Models;

namespace ExcelToSqlClients.Infrastructure.Import.Excel;

public sealed class ClosedXmlClientReader : IClientReader
{
    public IEnumerable<ClientReadResult> Read(string path)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet(1);

        var map = ExcelHeaderMap.FromHeaderRow(ws.Row(1));
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (int r = 2; r <= lastRow; r++)
        {
            var row = ws.Row(r);
            var errors = new List<ImportError>();

            // обязательное поле
            var cardCode = ReadRequiredString(map, row, r, "CardCode", errors);
            if (errors.Count > 0)
            {
                yield return new ClientReadResult { RowNumber = r, Error = errors[0] };
                continue;
            }

            var client = new Client
            {
                CardCode = cardCode!,
                LastName = ReadString(map, row, "LastName"),
                FirstName = ReadString(map, row, "FirstName"),
                SurName = ReadString(map, row, "SurName"),
                City = ReadString(map, row, "City"),
                Email = ReadString(map, row, "Email"),
                Pincode = ReadString(map, row, "Pincode"),
            };

            // опциональные поля
            client.PhoneMobile = ReadIfPresent(map, row, "PhoneMobile",
                cell => ExcelValueParsers.ReadPhoneAsString(cell));

            client.GenderId = ReadIfPresent(map, row, "GenderId",
                cell => ExcelValueParsers.ReadGenderIdNullable(cell, r, "GenderId", errors));

            client.Birthday = ReadIfPresent(map, row, "Birthday",
                cell => ExcelValueParsers.ReadDateOnlyNullable(cell, r, "Birthday", errors));

            client.Bonus = ReadIfPresent(map, row, "Bonus",
                cell => ExcelValueParsers.ReadIntNullable(cell, r, "Bonus", errors));

            client.Turnover = ReadIfPresent(map, row, "Turnover",
                cell => ExcelValueParsers.ReadLongNullable(cell, r, "Turnover", errors));

            if (errors.Count > 0)
            {
                yield return new ClientReadResult
                {
                    RowNumber = r,
                    Client = client,
                    Error = errors[0]
                };
                continue;
            }

            yield return new ClientReadResult { RowNumber = r, Client = client };
        }
    }

    private static string? ReadString(ExcelHeaderMap map, IXLRow row, string header)
        => ReadIfPresent(map, row, header, ExcelValueParsers.ReadString);

    private static T? ReadIfPresent<T>(ExcelHeaderMap map, IXLRow row, string header, Func<IXLCell, T?> reader)
        => map.TryGetColumn(header, out var col) ? reader(row.Cell(col)) : default;

    private static string? ReadRequiredString(ExcelHeaderMap map, IXLRow row, int r, string header, List<ImportError> errors)
    {
        var raw = ReadString(map, row, header);
        var value = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();

        if (value is null)
            errors.Add(new ImportError { RowNumber = r, Field = header, RawValue = raw ?? "", Message = $"{header} is required" });

        return value;
    }
}