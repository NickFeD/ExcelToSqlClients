using Dapper;
using Microsoft.Data.SqlClient;

namespace ExcelToSqlClients.Dynamic.Import.Console;

public sealed class DynamicRepository
{
    public async Task<(int inserted, int updated)> UpsertBatchAsync(
        SqlConnection con,
        DynamicSchemaConfig schema,
        string keyCol,
        string insertSql,
        string updateSql,
        IReadOnlyList<DynamicRowReadResult> batch,
        CancellationToken ct)
    {
        // last wins per key
        var byKey = new Dictionary<string, DynamicRowReadResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var it in batch) byKey[it.Key] = it;

        var keys = byKey.Keys.ToList();

        var existsSql = $@"SELECT [{EscId(keyCol)}] FROM [{EscId(schema.Schema)}].[{EscId(schema.Table)}]
WHERE [{EscId(keyCol)}] IN @Keys;";

        var existingKeys = (await con.QueryAsync<object>(
            new CommandDefinition(existsSql, new { Keys = keys }, cancellationToken: ct)))
            .Select(x => x.ToString() ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toInsert = new List<object>();
        var toUpdate = new List<object>();

        foreach (var kv in byKey)
        {
            if (kv.Value.Params == null) continue;
            if (existingKeys.Contains(kv.Key)) toUpdate.Add(kv.Value.Params);
            else toInsert.Add(kv.Value.Params);
        }

        using var tx = con.BeginTransaction();
        try
        {
            int inserted = 0, updated = 0;

            if (toInsert.Count > 0)
            {
                await con.ExecuteAsync(new CommandDefinition(insertSql, toInsert, tx, cancellationToken: ct));
                inserted = toInsert.Count;
            }

            if (toUpdate.Count > 0)
            {
                await con.ExecuteAsync(new CommandDefinition(updateSql, toUpdate, tx, cancellationToken: ct));
                updated = toUpdate.Count;
            }

            tx.Commit();
            return (inserted, updated);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static string EscId(string s) => (s ?? "").Replace("]", "]]" );
}
