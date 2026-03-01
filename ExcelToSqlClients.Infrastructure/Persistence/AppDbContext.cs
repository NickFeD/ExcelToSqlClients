using ExcelToSqlClients.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcelToSqlClients.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public DbSet<Client> Clients => Set<Client>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}