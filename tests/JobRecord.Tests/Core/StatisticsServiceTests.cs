using FluentAssertions;
using JobRecord.Core.Dtos;
using JobRecord.Core.Enums;
using JobRecord.Core.Models;
using JobRecord.Core.Services;
using JobRecord.Tests.Common;

namespace JobRecord.Tests.Core;

public sealed class StatisticsServiceTests
{
    [Fact]
    public async Task GetTodaySummaryAsync_ShouldSplitCrossDayEntries()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 30, 8, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();

        var task = new TaskItem
        {
            Title = "跨天任务",
            Priority = TaskPriority.P1,
            Status = TaskStatus.Completed,
            CreatedAt = clock.Now,
            UpdatedAt = clock.Now,
            CompletedAt = new DateTimeOffset(2026, 5, 30, 1, 0, 0, TimeSpan.Zero)
        };
        db.Context.Add(task);
        db.Context.Add(new TimeEntry
        {
            TaskItemId = task.Id,
            StartAt = new DateTimeOffset(2026, 5, 29, 23, 30, 0, TimeSpan.Zero),
            EndAt = new DateTimeOffset(2026, 5, 30, 1, 0, 0, TimeSpan.Zero),
            DurationSeconds = 5400,
            CreatedAt = clock.Now
        });
        await db.Context.SaveChangesAsync();

        var service = new StatisticsService(db.Context, clock);
        var summary = await service.GetTodaySummaryAsync(new DateOnly(2026, 5, 30));

        summary.TotalDuration.Should().Be(TimeSpan.FromHours(1));
        summary.CompletedTaskCount.Should().Be(1);
        summary.PriorityBreakdown.Single(item => item.Priority == TaskPriority.P1).Duration.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task GetCurrentTaskDisplayDurationAsync_ShouldIncludeStoredAndLiveDuration()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 30, 9, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();

        var task = new TaskItem
        {
            Title = "实时任务",
            Priority = TaskPriority.P2,
            Status = TaskStatus.Running,
            CreatedAt = clock.Now.AddHours(-1),
            UpdatedAt = clock.Now.AddHours(-1)
        };

        db.Context.Add(task);
        db.Context.Add(new TimeEntry
        {
            TaskItemId = task.Id,
            StartAt = clock.Now.AddMinutes(-30),
            EndAt = clock.Now.AddMinutes(-20),
            DurationSeconds = 600,
            CreatedAt = clock.Now.AddMinutes(-30)
        });
        db.Context.Add(new TimeEntry
        {
            TaskItemId = task.Id,
            StartAt = clock.Now.AddMinutes(-10),
            EndAt = null,
            DurationSeconds = 0,
            CreatedAt = clock.Now.AddMinutes(-10)
        });
        await db.Context.SaveChangesAsync();

        var service = new StatisticsService(db.Context, clock);
        var duration = await service.GetCurrentTaskDisplayDurationAsync(task.Id);

        duration.Should().Be(TimeSpan.FromMinutes(20));
    }
}
