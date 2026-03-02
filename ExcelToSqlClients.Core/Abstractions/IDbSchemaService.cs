using ExcelToSqlClients.Core.Models.Db;

namespace ExcelToSqlClients.Core.Abstractions;

public interface IDbSchemaService
{
    Task<IReadOnlyList<DbTableInfo>> GetTablesAsync(CancellationToken ct);
    Task<DbTableSchema> GetTableSchemaAsync(DbTableInfo table, CancellationToken ct);
}
