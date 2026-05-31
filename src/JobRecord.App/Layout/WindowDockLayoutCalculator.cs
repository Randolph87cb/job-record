using System.Windows;
using JobRecord.Core.Enums;

namespace JobRecord.App.Layout;

public readonly record struct WindowDockPlacement(DockMode DockMode, double Left, double Top, bool IsSnapped);

public static class WindowDockLayoutCalculator
{
    public const double DefaultSnapThreshold = 24;

    public static WindowDockPlacement ResolveDragRelease(
        Rect workArea,
        System.Windows.Size windowSize,
        double left,
        double top,
        double marginTop,
        double marginSide,
        double snapThreshold = DefaultSnapThreshold)
    {
        var topDistance = top - workArea.Top;
        var leftDistance = left - workArea.Left;
        var rightDistance = workArea.Right - (left + windowSize.Width);

        if (topDistance <= snapThreshold)
        {
            return GetPlacementForMode(workArea, windowSize, DockMode.TopCenter, marginTop, marginSide);
        }

        var canSnapLeft = leftDistance <= snapThreshold;
        var canSnapRight = rightDistance <= snapThreshold;

        if (canSnapLeft && canSnapRight)
        {
            return leftDistance <= rightDistance
                ? GetPlacementForMode(workArea, windowSize, DockMode.LeftEdge, marginTop, marginSide)
                : GetPlacementForMode(workArea, windowSize, DockMode.RightEdge, marginTop, marginSide);
        }

        if (canSnapLeft)
        {
            return GetPlacementForMode(workArea, windowSize, DockMode.LeftEdge, marginTop, marginSide);
        }

        if (canSnapRight)
        {
            return GetPlacementForMode(workArea, windowSize, DockMode.RightEdge, marginTop, marginSide);
        }

        return new WindowDockPlacement(
            DockMode.Floating,
            Clamp(left, workArea.Left, workArea.Right - windowSize.Width),
            Clamp(top, workArea.Top, workArea.Bottom - windowSize.Height),
            false);
    }

    public static WindowDockPlacement GetPlacementForMode(
        Rect workArea,
        System.Windows.Size windowSize,
        DockMode dockMode,
        double marginTop,
        double marginSide,
        double? savedLeft = null,
        double? savedTop = null)
        => dockMode switch
        {
            DockMode.LeftEdge => new WindowDockPlacement(
                DockMode.LeftEdge,
                workArea.Left + marginSide,
                workArea.Top + Math.Max(0, (workArea.Height - windowSize.Height) / 2),
                true),
            DockMode.RightEdge => new WindowDockPlacement(
                DockMode.RightEdge,
                workArea.Right - windowSize.Width - marginSide,
                workArea.Top + Math.Max(0, (workArea.Height - windowSize.Height) / 2),
                true),
            DockMode.Floating => new WindowDockPlacement(
                DockMode.Floating,
                Clamp(savedLeft ?? workArea.Left + (workArea.Width - windowSize.Width) / 2, workArea.Left, workArea.Right - windowSize.Width),
                Clamp(savedTop ?? workArea.Top + marginTop, workArea.Top, workArea.Bottom - windowSize.Height),
                false),
            _ => new WindowDockPlacement(
                DockMode.TopCenter,
                workArea.Left + (workArea.Width - windowSize.Width) / 2,
                workArea.Top + marginTop,
                true)
        };

    private static double Clamp(double value, double min, double max)
    {
        if (max < min)
        {
            return min;
        }

        return Math.Min(Math.Max(value, min), max);
    }
}
