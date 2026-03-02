using Dapper;
using Microsoft.Data.SqlClient;

namespace ExcelToSqlClients.Dynamic.Import.Console;

public class SchemaApplier
{
    private readonly SqlConnectionStringHelper _csHelper;

    public SchemaApplier(SqlConnectionStringHelper csHelper) => _csHelper = csHelper;

    public async Task ApplyAsync(string cs, DynamicSchemaConfig schema, CancellationToken ct)
    {
        // Ensure DB
        var dbName = _csHelper.GetDatabaseName(cs);
        if (string.IsNullOrWhiteSpace(dbName))
            throw new InvalidOperationException("Database is missing in connection string.");

        await using (var conMaster = new SqlConnection(_csHelper.ToMaster(cs)))
        {
            await conMaster.OpenAsync(ct);
            var sql = $@"
IF DB_ID(N'{EscLit(dbName)}') IS NULL
    CREATE DATABASE [{EscId(dbName)}];";
            await conMaster.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
        }

        await using var con = new SqlConnection(cs);
        await con.OpenAsync(ct);

        // Ensure schema
        var ensureSchema = $@"
IF SCHEMA_ID(N'{EscLit(schema.Schema)}') IS NULL
    EXEC('CREATE SCHEMA [{EscId(schema.Schema)}]');";
        await con.ExecuteAsync(new CommandDefinition(ensureSchema, cancellationToken: ct));

        // Ensure table
        var createTable = BuildCreateTable(schema);
        await con.ExecuteAsync(new CommandDefinition(createTable, cancellationToken: ct));

        // Add missing columns
        var existingCols = await GetExistingColumnsAsync(con, schema.Schema, schema.Table, ct);
        foreach (var col in schema.Columns)
        {
            if (existingCols.Contains(col.Name, StringComparer.OrdinalIgnoreCase))
                continue;

            var add = $@"ALTER TABLE {Full(schema.Schema, schema.Table)}
ADD [{EscId(col.Name)}] {col.SqlType} {(col.Nullable ? "NULL" : "NOT NULL")};";
            await con.ExecuteAsync(new CommandDefinition(add, cancellationToken: ct));
        }

        // PK
        var pkCols = schema.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
        if (pkCols.Count > 0)
        {
            var pkExists = await con.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1)
FROM sys.key_constraints kc
JOIN sys.tables t ON t.object_id = kc.parent_object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE kc.type='PK' AND s.name=@Schema AND t.name=@Table;",
                new { Schema = schema.Schema, Table = schema.Table }, cancellationToken: ct));

            if (pkExists == 0)
            {
                var pkName = $"PK_{schema.Table}";
                var colsSql = string.Join(", ", pkCols.Select(c => $"[{EscId(c)}]"));
                var addPk = $@"ALTER TABLE {Full(schema.Schema, schema.Table)}
ADD CONSTRAINT [{EscId(pkName)}] PRIMARY KEY ({colsSql});";
                await con.ExecuteAsync(new CommandDefinition(addPk, cancellationToken: ct));
            }
        }

        // Unique indexes (single-column, как в конфиге)
        foreach (var col in schema.Columns.Where(c => c.IsUnique))
        {
            var ixName = $"UX_{schema.Table}_{col.Name}";
            var exists = await con.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1)
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name=@Schema AND t.name=@Table AND i.name=@IndexName;",
                new { Schema = schema.Schema, Table = schema.Table, IndexName = ixName }, cancellationToken: ct));

            if (exists == 0)
            {
                var addIx = $@"CREATE UNIQUE INDEX [{EscId(ixName)}]
ON {Full(schema.Schema, schema.Table)} ([{EscId(col.Name)}]);";
                await con.ExecuteAsync(new CommandDefinition(addIx, cancellationToken: ct));
            }
        }
    }

    private static string BuildCreateTable(DynamicSchemaConfig schema)
    {
        var cols = schema.Columns.Select(c => $"[{EscId(c.Name)}] {c.SqlType} {(c.Nullable ? "NULL" : "NOT NULL")}");
        var colsSql = string.Join(",\n    ", cols);

        return $@"
IF OBJECT_ID(N'{EscLit(schema.Schema)}.{EscLit(schema.Table)}', N'U') IS NULL
BEGIN
    CREATE TABLE {Full(schema.Schema, schema.Table)}
    (
    {colsSql}
    );
END";
    }

    private static async Task<HashSet<string>> GetExistingColumnsAsync(SqlConnection con, string schema, string table, CancellationToken ct)
    {
        const string sql = @"
SELECT c.name
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = @Schema AND t.name = @Table;";
        var rows = await con.QueryAsync<string>(new CommandDefinition(sql, new { Schema = schema, Table = table }, cancellationToken: ct));
        return rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string Full(string schema, string table) => $"[{EscId(schema)}].[{EscId(table)}]";
    private static string EscId(string s) => (s ?? "").Replace("]", "]]");
    private static string EscLit(string s) => (s ?? "").Replace("'", "''");
}