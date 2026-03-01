using ExcelToSqlClients.Core.Entities;

namespace ExcelToSqlClients.Core.Abstractions;

public interface IClientRepository
{
    Task<(int inserted, int updated)> UpsertBatchAsync(IReadOnlyList<Client> batch, CancellationToken ct);
}