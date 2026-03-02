namespace ExcelToSqlClients.Dynamic.Import.Console;

public class DynamicColumnConfig
{
    public string Name { get; set; } = "";
    public string SqlType { get; set; } = "nvarchar(200)";
    public bool Nullable { get; set; } = true;

    public bool IsPrimaryKey { get; set; } = false;
    public bool IsUnique { get; set; } = false;
}