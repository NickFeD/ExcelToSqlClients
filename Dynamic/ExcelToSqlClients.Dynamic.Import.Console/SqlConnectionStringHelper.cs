using Microsoft.Data.SqlClient;

namespace ExcelToSqlClients.Dynamic.Import.Console;

public class SqlConnectionStringHelper
{
    public string GetDatabaseName(string cs)
    {
        var b = new SqlConnectionStringBuilder(cs);
        return b.InitialCatalog;
    }

    public string ToMaster(string cs)
    {
        var b = new SqlConnectionStringBuilder(cs);
        b.InitialCatalog = "master";
        return b.ConnectionString;
    }
}