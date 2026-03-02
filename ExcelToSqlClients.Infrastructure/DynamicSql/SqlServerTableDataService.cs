using Dapper;
using ExcelToSqlClients.Core.Abstractions;
using ExcelToSqlClients.Core.Models.Db;

namespace ExcelToSqlClients.Infrastructure.DynamicSql;

public sealed class SqlServerTableDataService : IDbTableDataService
{
    private readonly SqlConnectionFactory _factory;
    private readonly IDbSchemaService _schema;

    public SqlServerTableDataService(SqlConnectionFactory factory, IDbSchemaService schema)
    {
        _factory = factory;
        _schema = schema;
    }

    public async Task<DbPageResult> ReadPageAsync(DbTableInfo table, int skip, int take, CancellationToken ct)
    {
        var schema = await _schema.GetTableSchemaAsync(table, ct);

        var orderByCol = schema.PrimaryKeyColumns.FirstOrDefault() ?? schema.Columns.First().Name;

        var colsSql = string.Join(", ", schema.Columns.Select(c => $"[{c.Name}]"));
        var sql = $@"
SELECT {colsSql}
FROM {table.FullName}
ORDER BY [{orderByCol}]
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";

        using var con = _factory.Create();

        var rows = await con.QueryAsync(new CommandDefinition(sql, new { Skip = skip, Take = take }, cancellationToken: ct));
        var list = new List<Dictionary<string, object?>>();

        foreach (var r in rows)
        {
            var dict = (IDictionary<string, object?>)r;
            list.Add(dict.ToDictionary(k => k.Key, v => v.Value));
        }

        return new DbPageResult
        {
            Schema = schema,
            Rows = list,
            Skip = skip,
            Take = take,
            HasMore = list.Count == take
        };
    }

    public async Task<DbSaveResult> SaveChangesAsync(
        DbTableInfo table,
        DbTableSchema schema,
        IReadOnlyList<Dictionary<string, object?>> newRows,
        IReadOnlyList<(Dictionary<string, object?> original, Dictionary<string, object?> current)> changedRows,
        CancellationToken ct)
    {
        bool canUpdate = schema.PrimaryKeyColumns.Count > 0;

        int inserted = 0;
        int updated = 0;

        var insertableCols = schema.Columns
            .Where(c => !c.IsIdentity && !c.IsComputed)
            .Select(c => c.Name)
            .ToList();

        var updatableCols = schema.Columns
            .Where(c => !c.IsIdentity && !c.IsComputed && !schema.PrimaryKeyColumns.Contains(c.Name))
            .Select(c => c.Name)
            .ToList();

        using var con = _factory.Create();
        await con.OpenAsync(ct);
        using var tx = con.BeginTransaction();

        try
        {
            foreach (var row in newRows)
            {
                var cols = insertableCols;
                if (cols.Count == 0) continue;

                var colListSql = string.Join(", ", cols.Select(c => $"[{c}]"));
                var paramListSql = string.Join(", ", cols.Select(c => $"@{c}"));

                var identityCol = schema.Columns.FirstOrDefault(c => c.IsIdentity)?.Name;

                var sql = identityCol != null
                    ? $@"INSERT INTO {table.FullName} ({colListSql})
                        OUTPUT INSERTED.[{identityCol}]
                        VALUES ({paramListSql});"
                    : $@"INSERT INTO {table.FullName} ({colListSql})
                        VALUES ({paramListSql});";

                var p = new DynamicParameters();
                foreach (var c in cols)
                    p.Add(c, row.TryGetValue(c, out var v) ? v : null);

                if (identityCol != null)
                {
                    var newId = await con.ExecuteScalarAsync<object?>(
                        new CommandDefinition(sql, p, tx, cancellationToken: ct));

                    row[identityCol] = newId;
                }
                else
                {
                    await con.ExecuteAsync(new CommandDefinition(sql, p, tx, cancellationToken: ct));
                }

                inserted++;
            }

            if (canUpdate)
            {
                foreach (var (original, current) in changedRows)
                {
                    var changedCols = updatableCols
                        .Where(c => !Equals(NormalizeDbValue(original, c), NormalizeDbValue(current, c)))
                        .ToList();

                    if (changedCols.Count == 0)
                        continue;

                    var setSql = string.Join(", ", changedCols.Select(c => $"[{c}] = @{c}"));
                    var whereSql = string.Join(" AND ", schema.PrimaryKeyColumns.Select(k => $"[{k}] = @__pk_{k}"));

                    var sql = $@"UPDATE {table.FullName}
                                 SET {setSql}
                                 WHERE {whereSql};";

                    var p = new DynamicParameters();

                    foreach (var c in changedCols)
                        p.Add(c, current.TryGetValue(c, out var v) ? v : null);

                    foreach (var k in schema.PrimaryKeyColumns)
                        p.Add("__pk_" + k, original.TryGetValue(k, out var pkv) ? pkv : null);

                    var affected = await con.ExecuteAsync(new CommandDefinition(sql, p, tx, cancellationToken: ct));
                    if (affected > 0) updated++;
                }
            }

            tx.Commit();
            return new DbSaveResult { Inserted = inserted, Updated = updated };
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static object? NormalizeDbValue(Dictionary<string, object?> dict, string col)
        => dict.TryGetValue(col, out var v) ? v : null;
}