namespace ExcelToSqlClients.Core.Models.Db;

public sealed class DbTableInfo
{
    public string Schema { get; init; } = "dbo";
    public string Name { get; init; } = "";
    public string FullName => $"[{Schema}].[{Name}]";

    public override string ToString() => FullName;
}