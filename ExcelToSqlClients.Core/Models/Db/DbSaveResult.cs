namespace ExcelToSqlClients.Core.Models.Db;

public sealed class DbSaveResult
{
    public int Inserted { get; init; }
    public int Updated { get; init; }
}