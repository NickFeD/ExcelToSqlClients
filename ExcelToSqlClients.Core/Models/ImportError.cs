namespace ExcelToSqlClients.Core.Models;

public sealed class ImportError
{
    public int RowNumber { get; init; }
    public string Field { get; init; } = "";
    public string RawValue { get; init; } = "";
    public string Message { get; init; } = "";
}
