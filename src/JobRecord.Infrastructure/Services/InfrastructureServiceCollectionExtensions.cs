using JobRecord.Core.Abstractions;
using JobRecord.Core.Services;
using JobRecord.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobRecord.Infrastructure.Services;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddJobRecordInfrastructure(this IServiceCollection services, string? databasePath = null)
    {
        var resolvedPath = string.IsNullOrWhiteSpace(databasePath) ? ResolveDefaultDatabasePath() : databasePath;
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        services.AddDbContext<JobRecordDbContext>(options =>
            options.UseSqlite($"Data Source={resolvedPath}"));

        services.AddScoped<IJobRecordDbContext>(provider => provider.GetRequiredService<JobRecordDbContext>());
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ITimerService, TimerService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IRuntimeRecoveryService, RuntimeRecoveryService>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }

    public static string ResolveDefaultDatabasePath()
    {
        var baseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JobRecord");

        return Path.Combine(baseFolder, "jobrecord.db");
    }
}
