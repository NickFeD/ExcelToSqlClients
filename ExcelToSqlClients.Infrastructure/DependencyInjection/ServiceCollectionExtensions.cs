using ExcelToSqlClients.Core.Abstractions;
using ExcelToSqlClients.Infrastructure.DynamicSql;
using ExcelToSqlClients.Infrastructure.Import.Excel;
using ExcelToSqlClients.Infrastructure.Persistence;
using ExcelToSqlClients.Infrastructure.Repositories;
using ExcelToSqlClients.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExcelToSqlClients.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionStrings:Default is missing.");


        services.AddDbContextFactory<AppDbContext>(opt =>
        {
            opt.UseSqlServer(cs);
        });

        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IClientReader, ClosedXmlClientReader>();
        services.AddScoped<IClientImportService, ClientImportService>();

        services.AddScoped<IClientCrudService, ClientCrudService>();

        services.AddSingleton(new SqlConnectionFactory(cs));
        services.AddSingleton<IDbSchemaService, SqlServerSchemaService>();
        services.AddSingleton<IDbTableDataService, SqlServerTableDataService>();

        return services;
    }
}