using FluentAssertions;
using JobRecord.Core.Enums;
using JobRecord.Core.Models;
using JobRecord.Core.Services;
using JobRecord.Tests.Common;

namespace JobRecord.Tests.Core;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task SettingsService_ShouldPersistVisibilityExpandedAndWindowPlacement()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 29, 14, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();
        var service = new SettingsService(db.Context, clock);

        var settings = await service.GetSettingsAsync();
        settings.DockMode = DockMode.RightEdge;
        settings.BarWidth = 500;
        settings.BarHeight = 40;
        settings.WindowLeft = 123;
        settings.WindowTop = 16;

        await service.SaveSettingsAsync(settings);
        await service.SetBarVisibilityAsync(false);
        await service.SetExpandedStateAsync(true);
        await service.SaveWindowPlacementAsync(222, 18);

        var storedSettings = await service.GetSettingsAsync();
        var runtimeState = await service.GetRuntimeStateAsync();

        storedSettings.DockMode.Should().Be(DockMode.TopCenter);
        storedSettings.WindowLeft.Should().Be(222);
        storedSettings.WindowTop.Should().Be(18);
        runtimeState.IsBarVisible.Should().BeFalse();
        runtimeState.IsExpanded.Should().BeTrue();
    }
}
