using ExcelToSqlClients.Core.Abstractions;
using ExcelToSqlClients.Core.Models.Db;
using Dapper;

namespace ExcelToSqlClients.Infrastructure.DynamicSql;

public sealed class SqlServerSchemaService : IDbSchemaService
{
    private readonly SqlConnectionFactory _factory;

    public SqlServerSchemaService(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<DbTableInfo>> GetTablesAsync(CancellationToken ct)
    {
        const string sql = @"
SELECT s.name AS [Schema], t.name AS [Name]
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
ORDER BY s.name, t.name;";

        using var con = _factory.Create();
        var rows = await con.QueryAsync<DbTableInfo>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<DbTableSchema> GetTableSchemaAsync(DbTableInfo table, CancellationToken ct)
    {
        const string sqlCols = @"
SELECT
  c.name AS [Name],
  ty.name AS [SqlType],
  c.is_nullable AS [IsNullable],
  c.is_identity AS [IsIdentity],
  c.is_computed AS [IsComputed],
  c.max_length AS [MaxLength],
  c.precision AS [Precision],
  c.scale AS [Scale]
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE s.name = @Schema AND t.name = @Table
ORDER BY c.column_id;";

        const string sqlPk = @"
SELECT c.name
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.indexes i ON i.object_id = t.object_id AND i.is_primary_key = 1
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE s.name = @Schema AND t.name = @Table
ORDER BY ic.key_ordinal;";

        using var con = _factory.Create();

        var cols = (await con.QueryAsync<DbColumnInfo>(
            new CommandDefinition(sqlCols, new { Schema = table.Schema, Table = table.Name }, cancellationToken: ct)
        )).ToList();

        var pk = (await con.QueryAsync<string>(
            new CommandDefinition(sqlPk, new { Schema = table.Schema, Table = table.Name }, cancellationToken: ct)
        )).ToList();

        return new DbTableSchema
        {
            Table = table,
            Columns = cols,
            PrimaryKeyColumns = pk
        };
    }
}