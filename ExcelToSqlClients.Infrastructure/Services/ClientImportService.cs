using ExcelToSqlClients.Core.Abstractions;
using ExcelToSqlClients.Core.Entities;
using ExcelToSqlClients.Core.Models;

namespace ExcelToSqlClients.Infrastructure.Services;

public sealed class ClientImportService : IClientImportService
{
    private readonly IClientReader _reader;
    private readonly IClientRepository _repo;

    public ClientImportService(IClientReader reader, IClientRepository repo)
    {
        _reader = reader;
        _repo = repo;
    }

    public async Task<ImportResult> ImportAsync(string excelPath, int batchSize, CancellationToken ct)
    {
        if (batchSize <= 0)
            batchSize = 1000;

        var result = new ImportResult();
        var batch = new List<Client>(batchSize);

        foreach (var read in _reader.Read(excelPath))
        {
            ct.ThrowIfCancellationRequested();
            result.ReadTotal++;

            if (!read.IsSuccess)
            {
                result.Skipped++;
                if (read.Error != null) result.Errors.Add(read.Error);
                continue;
            }

            // если Client есть, но Error тоже есть - считаем как skipped
            if (read.Client == null)
            {
                result.Skipped++;
                continue;
            }

            batch.Add(read.Client);

            if (batch.Count >= batchSize)
            {
                var (inserted, updated) = await _repo.UpsertBatchAsync(batch, ct);
                result.Inserted += inserted;
                result.Updated += updated;
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            var (inserted, updated) = await _repo.UpsertBatchAsync(batch, ct);
            result.Inserted += inserted;
            result.Updated += updated;
            batch.Clear();
        }

        return result;
    }
}