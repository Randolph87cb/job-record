using FluentAssertions;
using JobRecord.Core.Enums;
using JobRecord.Core.Models;
using JobRecord.Core.Services;
using JobRecord.Tests.Common;

namespace JobRecord.Tests.Core;

public sealed class RuntimeRecoveryServiceTests
{
    [Fact]
    public async Task RecoverAsync_ShouldCloseOpenEntriesAndPauseTasks()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 29, 12, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();

        var task = new TaskItem
        {
            Title = "异常退出任务",
            Priority = TaskPriority.P2,
            Status = TaskStatus.Running,
            CreatedAt = clock.Now.AddHours(-2),
            UpdatedAt = clock.Now.AddHours(-2),
            StartedAt = clock.Now.AddHours(-2)
        };

        db.Context.Add(task);
        db.Context.Add(new TimeEntry
        {
            TaskItemId = task.Id,
            StartAt = clock.Now.AddHours(-2),
            EndAt = null,
            DurationSeconds = 0,
            CreatedAt = clock.Now.AddHours(-2)
        });
        await db.Context.SaveChangesAsync();

        var service = new RuntimeRecoveryService(db.Context, clock);
        var result = await service.RecoverAsync();

        result.FixedEntriesCount.Should().Be(1);
        result.PausedTasksCount.Should().Be(1);

        var restoredTask = db.Context.TaskItems.Single();
        restoredTask.Status.Should().Be(TaskStatus.Paused);
        db.Context.TimeEntriesSet.Single().EndAt.Should().Be(clock.Now);
    }
}
