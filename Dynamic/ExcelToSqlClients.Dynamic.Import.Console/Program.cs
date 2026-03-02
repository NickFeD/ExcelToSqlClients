using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExcelToSqlClients.Dynamic.Import.Console;

internal class Program
{
    static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(o => builder.Services.AddSingleton(o));

        builder.Services.AddSingleton<SchemaConfigProvider>();
        builder.Services.AddSingleton<SqlConnectionStringHelper>();
        builder.Services.AddSingleton<SchemaApplier>();

        // "как в оригинале": Reader -> ImportService -> Worker
        builder.Services.AddSingleton<IDynamicRowReader, ClosedXmlDynamicRowReader>();
        builder.Services.AddSingleton<DynamicRepository>();
        builder.Services.AddSingleton<DynamicImportService>();

        builder.Services.AddHostedService<Worker>();

        using var host = builder.Build();
        await host.RunAsync();
    }
}
