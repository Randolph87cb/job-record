using JobRecord.Core.Enums;
using JobRecord.Core.Models;

namespace JobRecord.Core.Abstractions;

public interface ISettingsService
{
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<RuntimeState> GetRuntimeStateAsync(CancellationToken cancellationToken = default);
    Task<AppSettings> SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task SetBarVisibilityAsync(bool isVisible, CancellationToken cancellationToken = default);
    Task SetExpandedStateAsync(bool isExpanded, CancellationToken cancellationToken = default);
    Task SaveWindowPlacementAsync(DockMode dockMode, double left, double top, CancellationToken cancellationToken = default);
}
