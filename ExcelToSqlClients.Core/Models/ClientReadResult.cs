using ExcelToSqlClients.Core.Entities;

namespace ExcelToSqlClients.Core.Models;

public sealed class ClientReadResult
{
    public int RowNumber { get; init; }
    public Client? Client { get; init; }
    public ImportError? Error { get; init; }

    public bool IsSuccess => Client != null && Error == null;
}