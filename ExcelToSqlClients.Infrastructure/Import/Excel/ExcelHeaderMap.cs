using ClosedXML.Excel;

namespace ExcelToSqlClients.Infrastructure.Import.Excel;

public sealed class ExcelHeaderMap
{
    private readonly Dictionary<string, int> _map;

    private ExcelHeaderMap(Dictionary<string, int> map)
    {
        _map = map;
    }

    public static ExcelHeaderMap FromHeaderRow(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var cell in headerRow.CellsUsed())
        {
            var header = NormalizeHeader(cell.GetString());
            if (string.IsNullOrWhiteSpace(header))
                continue;

            // ClosedXML column number starts from 1
            map[header] = cell.Address.ColumnNumber;
        }

        return new ExcelHeaderMap(map);
    }

    public bool TryGetColumn(string header, out int columnNumber)
        => _map.TryGetValue(NormalizeHeader(header), out columnNumber);

    private static string NormalizeHeader(string s)
        => (s ?? "").Trim().Replace("\u00A0", " "); // NBSP -> space
}