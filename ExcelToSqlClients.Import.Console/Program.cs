using CommandLine;
using ExcelToSqlClients.Infrastructure.DependencyInjection;
using ExcelToSqlClients.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExcelToSqlClients.Import.Console;

internal class Program
{
    static async Task Main(string[] args)
    {

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        // парсим аргументы и кладём Options в DI
        Parser.Default.ParseArguments<Options>(args)
              .WithParsed(opts => builder.Services.AddSingleton(opts));

        builder.Services.AddInfrastructure(builder.Configuration);

        // Worker
        builder.Services.AddHostedService<Worker>();
        builder.Services.AddLogging();

        using IHost host = builder.Build();

        await host.Services.GetRequiredService<AppDbContext>().Database.MigrateAsync();

        await host.RunAsync();
    }
}