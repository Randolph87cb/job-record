using JobRecord.Core.Enums;

namespace JobRecord.Core.Models;

public sealed class AppSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public DockMode DockMode { get; set; } = DockMode.TopCenter;
    public double BarWidth { get; set; } = 460;
    public double BarHeight { get; set; } = 34;
    public double MarginTop { get; set; } = 6;
    public double MarginSide { get; set; } = 12;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool AutoCollapseEnabled { get; set; } = true;
    public int AutoCollapseSeconds { get; set; } = 8;
    public bool LaunchAtStartup { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool IsBarVisible { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; }
}
