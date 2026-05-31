using FluentAssertions;
using JobRecord.App.Layout;
using JobRecord.Core.Enums;
using System.Windows;

namespace JobRecord.Tests.App;

public sealed class WindowDockLayoutCalculatorTests
{
    private static readonly Rect WorkArea = new(0, 0, 1920, 1080);
    private static readonly Size TopWindowSize = new(460, 120);
    private static readonly Size SideWindowSize = new(50, 280);

    [Fact]
    public void ResolveDragRelease_ShouldSnapToTopCenter_WhenNearTopEdge()
    {
        var placement = WindowDockLayoutCalculator.ResolveDragRelease(WorkArea, TopWindowSize, 200, 12, 6, 12);

        placement.DockMode.Should().Be(DockMode.TopCenter);
        placement.Left.Should().Be((WorkArea.Width - TopWindowSize.Width) / 2);
        placement.Top.Should().Be(6);
        placement.IsSnapped.Should().BeTrue();
    }

    [Fact]
    public void ResolveDragRelease_ShouldPreferTopOverLeft_WhenBothEdgesMatch()
    {
        var placement = WindowDockLayoutCalculator.ResolveDragRelease(WorkArea, TopWindowSize, 10, 8, 6, 12);

        placement.DockMode.Should().Be(DockMode.TopCenter);
    }

    [Fact]
    public void ResolveDragRelease_ShouldSnapToLeftEdge_WhenNearLeft()
    {
        var placement = WindowDockLayoutCalculator.ResolveDragRelease(WorkArea, SideWindowSize, 8, 320, 6, 12);

        placement.DockMode.Should().Be(DockMode.LeftEdge);
        placement.Left.Should().Be(12);
        placement.Top.Should().Be((WorkArea.Height - SideWindowSize.Height) / 2);
    }

    [Fact]
    public void ResolveDragRelease_ShouldSnapToRightEdge_WhenNearRight()
    {
        var placement = WindowDockLayoutCalculator.ResolveDragRelease(WorkArea, SideWindowSize, 1860, 320, 6, 12);

        placement.DockMode.Should().Be(DockMode.RightEdge);
        placement.Left.Should().Be(WorkArea.Right - SideWindowSize.Width - 12);
        placement.Top.Should().Be((WorkArea.Height - SideWindowSize.Height) / 2);
    }

    [Fact]
    public void ResolveDragRelease_ShouldRemainFloating_WhenNotNearAnyEdge()
    {
        var placement = WindowDockLayoutCalculator.ResolveDragRelease(WorkArea, TopWindowSize, 480, 280, 6, 12);

        placement.DockMode.Should().Be(DockMode.Floating);
        placement.Left.Should().Be(480);
        placement.Top.Should().Be(280);
        placement.IsSnapped.Should().BeFalse();
    }

    [Fact]
    public void GetPlacementForMode_ShouldKeepTopCentered_WhenWidthChanges()
    {
        var placement = WindowDockLayoutCalculator.GetPlacementForMode(WorkArea, new Size(320, 120), DockMode.TopCenter, 6, 12);

        placement.Left.Should().Be((WorkArea.Width - 320) / 2);
    }

    [Fact]
    public void GetPlacementForMode_ShouldKeepRightEdgeAligned_WhenDrawerExpands()
    {
        var placement = WindowDockLayoutCalculator.GetPlacementForMode(WorkArea, new Size(340, 320), DockMode.RightEdge, 6, 12);

        placement.Left.Should().Be(WorkArea.Right - 340 - 12);
        placement.Top.Should().Be((WorkArea.Height - 320) / 2);
    }
}
