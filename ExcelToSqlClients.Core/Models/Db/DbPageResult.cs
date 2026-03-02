namespace ExcelToSqlClients.Core.Models.Db;

public sealed class DbPageResult
{
    public DbTableSchema Schema { get; init; } = new();
    public List<Dictionary<string, object?>> Rows { get; init; } = new();

    public int Skip { get; init; }
    public int Take { get; init; }
    public bool HasMore { get; init; }
}
