using ExcelToSqlClients.Core.Abstractions;
using ExcelToSqlClients.Core.Entities;
using ExcelToSqlClients.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExcelToSqlClients.Infrastructure.Repositories;

public sealed class ClientRepository : IClientRepository
{
    private readonly AppDbContext _db;

    public ClientRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(int inserted, int updated)> UpsertBatchAsync(
        IReadOnlyList<Client> batch, CancellationToken ct)
    {
        static string? Norm(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        // CardCode может повторяться - берём последний
        var incomingByCode = batch
            .Select(x => (code: Norm(x.CardCode), client: x))
            .Where(x => x.code is not null)
            .GroupBy(x => x.code!)
            .ToDictionary(g => g.Key, g => g.Last().client);

        if (incomingByCode.Count == 0)
            return (0, 0);

        var codes = incomingByCode.Keys;

        var existing = await _db.Clients
            .Where(c => codes.Contains(c.CardCode))
            .ToDictionaryAsync(c => c.CardCode, ct);

        var inserted = 0;
        var updated = 0;

        foreach (var (code, incoming) in incomingByCode)
        {
            if (existing.TryGetValue(code, out var entity))
            {
                CopyFields(entity, incoming);
                updated++;
            }
            else
            {
                _db.Clients.Add(incoming);
                inserted++;
            }
        }

        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();

        return (inserted, updated);
    }

    private static void CopyFields(Client target, Client src)
    {
        // CardCode не меняем (ключ)
        target.LastName = src.LastName;
        target.FirstName = src.FirstName;
        target.SurName = src.SurName;

        target.PhoneMobile = src.PhoneMobile;
        target.Email = src.Email;

        target.GenderId = src.GenderId;
        target.Birthday = src.Birthday;

        target.City = src.City;
        target.Pincode = src.Pincode;

        target.Bonus = src.Bonus;
        target.Turnover = src.Turnover;
    }
}