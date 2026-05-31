using JobRecord.Core.Abstractions;
using JobRecord.Core.Models;

namespace JobRecord.Core.Services;

public sealed class SettingsService(IJobRecordDbContext dbContext, IClock clock) : ISettingsService
{
    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = dbContext.Settings.SingleOrDefault();
        if (settings is not null)
        {
            return settings;
        }

        settings = new AppSettings
        {
            UpdatedAt = clock.Now
        };

        dbContext.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public Task<RuntimeState> GetRuntimeStateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(GetOrCreateRuntimeState());

    public async Task<AppSettings> SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var existing = await GetSettingsAsync(cancellationToken);
        existing.DockMode = settings.DockMode;
        existing.BarWidth = settings.BarWidth;
        existing.BarHeight = settings.BarHeight;
        existing.MarginTop = settings.MarginTop;
        existing.MarginSide = settings.MarginSide;
        existing.WindowLeft = settings.WindowLeft;
        existing.WindowTop = settings.WindowTop;
        existing.AutoCollapseEnabled = settings.AutoCollapseEnabled;
        existing.AutoCollapseSeconds = settings.AutoCollapseSeconds;
        existing.LaunchAtStartup = settings.LaunchAtStartup;
        existing.MinimizeToTray = settings.MinimizeToTray;
        existing.IsBarVisible = settings.IsBarVisible;
        existing.UpdatedAt = clock.Now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task SetBarVisibilityAsync(bool isVisible, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        settings.IsBarVisible = isVisible;
        settings.UpdatedAt = clock.Now;

        var runtimeState = GetOrCreateRuntimeState();
        runtimeState.IsBarVisible = isVisible;
        runtimeState.UpdatedAt = clock.Now;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetExpandedStateAsync(bool isExpanded, CancellationToken cancellationToken = default)
    {
        var runtimeState = GetOrCreateRuntimeState();
        runtimeState.IsExpanded = isExpanded;
        runtimeState.LastActiveAt = clock.Now;
        runtimeState.UpdatedAt = clock.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveWindowPlacementAsync(Enums.DockMode dockMode, double left, double top, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        settings.DockMode = dockMode;
        settings.WindowLeft = left;
        settings.WindowTop = top;
        settings.UpdatedAt = clock.Now;

        var runtimeState = GetOrCreateRuntimeState();
        runtimeState.LastActiveAt = clock.Now;
        runtimeState.UpdatedAt = clock.Now;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private RuntimeState GetOrCreateRuntimeState()
    {
        var runtimeState = dbContext.RuntimeStates.SingleOrDefault();
        if (runtimeState is not null)
        {
            return runtimeState;
        }

        runtimeState = new RuntimeState
        {
            Id = RuntimeState.SingletonId,
            IsBarVisible = true,
            LastActiveAt = clock.Now,
            UpdatedAt = clock.Now
        };

        dbContext.Add(runtimeState);
        return runtimeState;
    }
}
