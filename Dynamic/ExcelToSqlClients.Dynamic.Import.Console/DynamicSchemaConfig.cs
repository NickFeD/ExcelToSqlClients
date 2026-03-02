namespace ExcelToSqlClients.Dynamic.Import.Console;

public class DynamicSchemaConfig
{
    public string Schema { get; set; } = "dbo";
    public string Table { get; set; } = "";
    public List<DynamicColumnConfig> Columns { get; set; } = new();
}
