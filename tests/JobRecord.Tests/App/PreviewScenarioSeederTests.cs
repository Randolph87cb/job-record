using FluentAssertions;
using JobRecord.App.Preview;
using JobRecord.Core.Enums;
using JobRecord.Tests.Common;

namespace JobRecord.Tests.App;

public sealed class PreviewScenarioSeederTests
{
    [Fact]
    public async Task SeedAsync_ShouldCreateRunningPreviewScenario()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 29, 9, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();
        var seeder = new PreviewScenarioSeeder(db.Context, clock);

        await seeder.SeedAsync(new PreviewLaunchOptions
        {
            IsEnabled = true,
            DockMode = DockMode.LeftEdge,
            IsExpanded = true,
            WorkState = PreviewWorkState.Running
        });

        db.Context.TaskItems.Count().Should().Be(5);
        db.Context.TimeEntriesSet.Count().Should().BeGreaterThan(0);

        var settings = db.Context.AppSettingsSet.Single();
        settings.DockMode.Should().Be(DockMode.LeftEdge);

        var runtime = db.Context.RuntimeStatesSet.Single();
        runtime.IsExpanded.Should().BeTrue();
        runtime.CurrentTaskId.Should().NotBeNull();

        db.Context.TaskItems.Count(task => task.Status == TaskStatus.Running).Should().Be(1);
        db.Context.TimeEntriesSet.Count(entry => entry.EndAt == null).Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateIdlePreviewScenarioWithoutActiveTask()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 29, 9, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();
        var seeder = new PreviewScenarioSeeder(db.Context, clock);

        await seeder.SeedAsync(new PreviewLaunchOptions
        {
            IsEnabled = true,
            DockMode = DockMode.TopCenter,
            IsExpanded = false,
            WorkState = PreviewWorkState.Idle
        });

        var runtime = db.Context.RuntimeStatesSet.Single();
        runtime.CurrentTaskId.Should().BeNull();
        db.Context.TaskItems
            .AsEnumerable()
            .Should()
            .OnlyContain(task => task.Status == TaskStatus.Pending || task.Status == TaskStatus.Completed);
        db.Context.TimeEntriesSet.Count(entry => entry.EndAt == null).Should().Be(0);
    }
}
