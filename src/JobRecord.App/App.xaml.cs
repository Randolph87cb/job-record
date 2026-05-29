using JobRecord.Core.Abstractions;
using JobRecord.App.Services;
using JobRecord.App.ViewModels;
using JobRecord.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace JobRecord.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private IServiceScope? _uiScope;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddJobRecordInfrastructure();
                    services.AddSingleton<IUserNotificationService, MessageBoxNotificationService>();
                    services.AddSingleton<TrayIconService>();
                    services.AddTransient<ShellViewModel>();
                    services.AddTransient<MainWindow>();
                })
                .Build();

            await _host.StartAsync();

            using (var initScope = _host.Services.CreateScope())
            {
                var initializer = initScope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                await initializer.InitializeAsync();

                var recoveryService = initScope.ServiceProvider.GetRequiredService<IRuntimeRecoveryService>();
                await recoveryService.RecoverAsync();
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
}
