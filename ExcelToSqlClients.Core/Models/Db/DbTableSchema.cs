namespace ExcelToSqlClients.Core.Models.Db;

public sealed class DbTableSchema
{
    public DbTableInfo Table { get; init; } = new();
    public List<DbColumnInfo> Columns { get; init; } = new();
    public List<string> PrimaryKeyColumns { get; init; } = new();
}
