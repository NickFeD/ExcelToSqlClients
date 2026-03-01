using ExcelToSqlClients.Core.Abstractions;
using ExcelToSqlClients.Core.Entities;
using ExcelToSqlClients.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExcelToSqlClients.Infrastructure.Services;

public sealed class ClientCrudService : IClientCrudService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public ClientCrudService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Client>> SearchAsync(string? query, int skip, int take, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        IQueryable<Client> q = db.Clients.AsNoTracking();

        query = (query ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            q = q.Where(x =>
                x.CardCode.Contains(query) ||
                (x.LastName ?? "").Contains(query) ||
                (x.FirstName ?? "").Contains(query) ||
                (x.SurName ?? "").Contains(query) ||
                (x.PhoneMobile ?? "").Contains(query));
        }

        //сортировка
        return await q.OrderBy(x => x.CardCode)
                      .Skip(skip)
                      .Take(take)
                      .ToListAsync(ct);
    }

    public async Task SaveAsync(IEnumerable<Client> clients, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var list = clients.ToList();
        if (list.Count == 0) return;

        // сохраняем только валидные у которых есть CardCode
        list = list.Where(x => !string.IsNullOrWhiteSpace(x.CardCode)).ToList();
        if (list.Count == 0) return;

        var codes = list.Select(x => x.CardCode).Distinct().ToList();

        var existing = await db.Clients
            .Where(x => codes.Contains(x.CardCode))
            .ToDictionaryAsync(x => x.CardCode, ct);

        foreach (var incoming in list)
        {
            if (existing.TryGetValue(incoming.CardCode, out var entity))
            {
                entity.LastName = incoming.LastName;
                entity.FirstName = incoming.FirstName;
                entity.SurName = incoming.SurName;
                entity.PhoneMobile = incoming.PhoneMobile;
                entity.Email = incoming.Email;
                entity.GenderId = incoming.GenderId;
                entity.Birthday = incoming.Birthday;
                entity.City = incoming.City;
                entity.Pincode = incoming.Pincode;
                entity.Bonus = incoming.Bonus;
                entity.Turnover = incoming.Turnover;
            }
            else
            {
                db.Clients.Add(incoming);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}