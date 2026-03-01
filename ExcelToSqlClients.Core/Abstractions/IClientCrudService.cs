using ExcelToSqlClients.Core.Entities;

namespace ExcelToSqlClients.Core.Abstractions;

public interface IClientCrudService
{
    Task<List<Client>> SearchAsync(string? query, int skip, int take, CancellationToken ct);
    Task SaveAsync(IEnumerable<Client> clients, CancellationToken ct);
}