using ExcelToSqlClients.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExcelToSqlClients.Dynamic.Import.Console;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly Options _options;
    private readonly SchemaConfigProvider _cfg;
    private readonly SchemaApplier _schema;
    private readonly DynamicImportService _importService;

    public Worker(
        ILogger<Worker> logger,
        IHostApplicationLifetime lifetime,
        Options options,
        SchemaConfigProvider cfg,
        SchemaApplier schema,
        DynamicImportService importService)
    {
        _logger = logger;
        _lifetime = lifetime;
        _options = options;
        _cfg = cfg;
        _schema = schema;
        _importService = importService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var cs = _cfg.GetConnectionString();
            var schema = _cfg.GetSchema();
            var batch = _options.BatchSize ?? _cfg.GetBatchSize();
            var ignoreErrors = _options.IgnoreErrors;

            _logger.LogInformation("Schema apply: [{schema}].[{table}]", schema.Schema, schema.Table);
            await _schema.ApplyAsync(cs, schema, stoppingToken);

            _logger.LogInformation("Import: file={file}, batch={batch}, ignoreErrors={ignore}", _options.ExcelPath, batch, ignoreErrors);
            ImportResult res = await _importService.ImportAsync(cs, schema, _options.ExcelPath, batch, ignoreErrors, stoppingToken);

            _logger.LogInformation("Done. Read={read} Inserted={ins} Updated={upd} Skipped={skip} Errors={err}",
                res.ReadTotal, res.Inserted, res.Updated, res.Skipped, res.Errors.Count);

            foreach (var e in res.Errors.Take(20))
                _logger.LogWarning("Row={row} Field={field} Raw='{raw}' Msg={msg}",
                    e.RowNumber, e.Field, e.RawValue, e.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed.");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
