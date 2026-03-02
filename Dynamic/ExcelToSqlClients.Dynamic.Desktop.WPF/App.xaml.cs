using ExcelToSqlClients.Dynamic.Desktop.WPF.ViewModels;
using ExcelToSqlClients.Dynamic.Desktop.WPF;
using ExcelToSqlClients.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace ExcelToSqlClients.Dynamic.Desktop.WPF;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.SetBasePath(AppContext.BaseDirectory);
                cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            })
            .ConfigureServices((ctx, services) =>
            {
                services.AddInfrastructure(ctx.Configuration);

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        _host.Services.GetRequiredService<MainWindow>().Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null) await _host.StopAsync();
        _host?.Dispose();
        base.OnExit(e);
    }
}