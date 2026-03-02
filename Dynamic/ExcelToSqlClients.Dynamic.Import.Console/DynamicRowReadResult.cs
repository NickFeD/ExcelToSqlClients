using Dapper;
using ExcelToSqlClients.Core.Models;

namespace ExcelToSqlClients.Dynamic.Import.Console;

public sealed class DynamicRowReadResult
{
    public int RowNumber { get; init; }
    public string Key { get; init; } = "";
    public DynamicParameters? Params { get; init; }
    public ImportError? Error { get; init; }

    public bool IsSuccess => Params != null && Error == null;
}
