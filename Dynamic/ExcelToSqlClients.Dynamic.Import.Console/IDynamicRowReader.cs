namespace ExcelToSqlClients.Dynamic.Import.Console;

public interface IDynamicRowReader
{
    IEnumerable<DynamicRowReadResult> Read(string path, DynamicSchemaConfig schema);
}
