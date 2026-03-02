using ExcelToSqlClients.Core.Models.Db;

namespace ExcelToSqlClients.Core.Abstractions;

public interface IDbTableDataService
{
    Task<DbPageResult> ReadPageAsync(DbTableInfo table, int skip, int take, CancellationToken ct);

    Task<DbSaveResult> SaveChangesAsync(
        DbTableInfo table,
        DbTableSchema schema,
        IReadOnlyList<Dictionary<string, object?>> newRows,
        IReadOnlyList<(Dictionary<string, object?> original, Dictionary<string, object?> current)> changedRows,
        CancellationToken ct);
}