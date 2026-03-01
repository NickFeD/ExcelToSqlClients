using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExcelToSqlClients.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Для миграций локальная строка
        var cs = Environment.GetEnvironmentVariable("EXCEL_SQLCLIENTS_CS")
                 ?? "Server=.;Database=ExcelToSqlClientsDb;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(cs)
            .Options;

        return new AppDbContext(options);
    }
}