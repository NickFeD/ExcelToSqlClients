using ExcelToSqlClients.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExcelToSqlClients.Import.Console;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IConfiguration _config;
    private readonly Options _options;
    private readonly IClientImportService _importService;

    public Worker(
        ILogger<Worker> logger,
        IHostApplicationLifetime lifetime,
        IConfiguration config,
        Options options,
        IClientImportService importService)
    {
        _logger = logger;
        _lifetime = lifetime;
        _config = config;
        _options = options;
        _importService = importService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            int batchSize = _options.BatchSize
                ?? _config.GetValue<int>("Import:BatchSize", 1000);

            _logger.LogInformation("Import started. File={file}, BatchSize={batch}", _options.ExcelPath, batchSize);

            var result = await _importService.ImportAsync(_options.ExcelPath, batchSize, stoppingToken);

            _logger.LogInformation("Import finished. ReadTotal={read}, Inserted={ins}, Updated={upd}, Skipped={skip}, Errors={err}",
                result.ReadTotal, result.Inserted, result.Updated, result.Skipped, result.Errors.Count);

            if (result.Errors.Count > 0)
            {
                foreach (var e in result.Errors.Take(20))
                {
                    _logger.LogWarning("Row={row} Field={field} Raw='{raw}' Msg={msg}",
                        e.RowNumber, e.Field, e.RawValue, e.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed");
        }
        finally
        {
            //отработали и выходим
            _lifetime.StopApplication();
        }
    }
}