using JobRecord.Core.Abstractions;
using JobRecord.App.Preview;
using JobRecord.App.Services;
using JobRecord.App.ViewModels;
using JobRecord.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;

namespace JobRecord.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private IServiceScope? _uiScope;
    private PreviewLaunchOptions _previewOptions = PreviewLaunchOptions.Disabled;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _previewOptions = PreviewLaunchOptions.Parse(e.Args);
            var databasePath = ResolveDatabasePath(_previewOptions);

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_previewOptions);
                    services.AddJobRecordInfrastructure(databasePath);
                    services.AddSingleton<IUserNotificationService, MessageBoxNotificationService>();
                    services.AddSingleton<TrayIconService>();
                    services.AddTransient<PreviewScenarioSeeder>();
                    services.AddTransient<ShellViewModel>();
                    services.AddTransient<MainWindow>();
                })
                .Build();

            await _host.StartAsync();

            using (var initScope = _host.Services.CreateScope())
            {
                var initializer = initScope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                await initializer.InitializeAsync();

                if (_previewOptions.IsEnabled)
                {
                    var seeder = initScope.ServiceProvider.GetRequiredService<PreviewScenarioSeeder>();
                    await seeder.SeedAsync(_previewOptions);
                }
                else
                {
                    var recoveryService = initScope.ServiceProvider.GetRequiredService<IRuntimeRecoveryService>();
                    await recoveryService.RecoverAsync();
                }
            }

            _uiScope = _host.Services.CreateScope();
            var mainWindow = _uiScope.ServiceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"应用启动失败：{ex.Message}",
                "工作时间记录",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _uiScope?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static string? ResolveDatabasePath(PreviewLaunchOptions previewOptions)
    {
        if (!previewOptions.IsEnabled)
        {
            return null;
        }

        var databasePath = previewOptions.GetDatabasePath();
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        return databasePath;
    }
}
