using JobRecord.Core.Enums;
using Brush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using BrushConverter = System.Windows.Media.BrushConverter;

namespace JobRecord.App.Styling;

public static class UiPalette
{
    public static Brush TextPrimary { get; } = CreateBrush("#FF13242D");
    public static Brush TextSecondary { get; } = CreateBrush("#FF5D717D");
    public static Brush TextTertiary { get; } = CreateBrush("#FF8194A0");
    public static Brush TimerText { get; } = CreateBrush("#FF0F2C39");
    public static Brush SurfaceAccent { get; } = CreateBrush("#FFF6FAFD");
    public static Brush SurfaceCurrent { get; } = CreateBrush("#FFF1F8FC");
    public static Brush BorderDefault { get; } = CreateBrush("#FFD6E3EA");
    public static Brush BorderStrong { get; } = CreateBrush("#FF95B6C7");
    public static Brush SuccessBackground { get; } = CreateBrush("#FFEAF7F1");
    public static Brush SuccessForeground { get; } = CreateBrush("#FF21644B");
    public static Brush PendingBackground { get; } = CreateBrush("#FFF0F5F8");
    public static Brush PendingForeground { get; } = CreateBrush("#FF4E6774");
    public static Brush CompletedBackground { get; } = CreateBrush("#FFF3F5F6");
    public static Brush CompletedForeground { get; } = CreateBrush("#FF6D7F88");

    public static Brush GetPriorityBackground(TaskPriority priority)
        => priority switch
        {
            TaskPriority.P1 => CreateBrush("#FFD95A58"),
            TaskPriority.P2 => CreateBrush("#FFCC8A36"),
            _ => CreateBrush("#FF457C97")
        };

    public static Brush GetPrioritySoftBackground(TaskPriority priority)
        => priority switch
        {
            TaskPriority.P1 => CreateBrush("#FFFCEDEC"),
            TaskPriority.P2 => CreateBrush("#FFFEF5E8"),
            _ => CreateBrush("#FFEDF6FB")
        };

    public static Brush GetPriorityForeground(TaskPriority priority)
        => priority switch
        {
            TaskPriority.P1 => CreateBrush("#FFFFFFFF"),
            TaskPriority.P2 => CreateBrush("#FF5B3A08"),
            _ => CreateBrush("#FFFFFFFF")
        };

    public static Brush GetStatusBackground(TaskStatus status)
        => status switch
        {
            TaskStatus.Running => CreateBrush("#FFE9F5F9"),
            TaskStatus.Paused => CreateBrush("#FFFEF2E7"),
            TaskStatus.Completed => CompletedBackground,
            _ => PendingBackground
        };

    public static Brush GetStatusForeground(TaskStatus status)
        => status switch
        {
            TaskStatus.Running => CreateBrush("#FF225B72"),
            TaskStatus.Paused => CreateBrush("#FF8A5415"),
            TaskStatus.Completed => CompletedForeground,
            _ => PendingForeground
        };

    private static SolidColorBrush CreateBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
