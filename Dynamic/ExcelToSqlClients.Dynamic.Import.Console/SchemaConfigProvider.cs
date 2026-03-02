using Microsoft.Extensions.Configuration;

namespace ExcelToSqlClients.Dynamic.Import.Console;

public sealed class SchemaConfigProvider
{
    private readonly IConfiguration _config;

    public SchemaConfigProvider(IConfiguration config) => _config = config;

    public string GetConnectionString()
        => _config.GetConnectionString("Default")
           ?? throw new InvalidOperationException("ConnectionStrings:Default is missing.");

    public int GetBatchSize()
        => _config.GetValue("Import:BatchSize", 1000);

    public DynamicSchemaConfig GetSchema()
    {
        var s = new DynamicSchemaConfig();
        _config.GetSection("DynamicSchema").Bind(s);

        if (string.IsNullOrWhiteSpace(s.Table))
            throw new InvalidOperationException("DynamicSchema:Table is missing.");

        if (s.Columns == null || s.Columns.Count == 0)
            throw new InvalidOperationException("DynamicSchema:Columns is empty.");

        foreach (var c in s.Columns)
        {
            if (string.IsNullOrWhiteSpace(c.Name))
                throw new InvalidOperationException("DynamicSchema:Columns has empty Name.");
            if (string.IsNullOrWhiteSpace(c.SqlType))
                throw new InvalidOperationException($"DynamicSchema:Columns '{c.Name}' has empty SqlType.");
        }

        var dup = s.Columns
            .GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (dup != null)
            throw new InvalidOperationException($"Duplicate column name in config: {dup.Key}");

        var keyCol = s.Columns.FirstOrDefault(c => c.IsPrimaryKey)?.Name
                  ?? s.Columns.FirstOrDefault(c => c.IsUnique)?.Name;

        if (string.IsNullOrWhiteSpace(keyCol))
            throw new InvalidOperationException("No key column. Mark one column IsPrimaryKey=true (or IsUnique=true).");

        return s;
    }
}
