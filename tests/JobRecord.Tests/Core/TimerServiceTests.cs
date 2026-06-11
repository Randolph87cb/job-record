using FluentAssertions;
using JobRecord.Core.Dtos;
using JobRecord.Core.Services;
using JobRecord.Tests.Common;

namespace JobRecord.Tests.Core;

public sealed class TimerServiceTests
{
    [Fact]
    public async Task TimerFlow_ShouldEnforceSingleActiveTaskAndSplitEntries()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 29, 9, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();
        var taskService = new TaskService(db.Context, clock);
        var timerService = new TimerService(db.Context, clock);

        var taskA = await taskService.CreateTaskAsync(new TaskCreateRequest { Title = "任务 A" });
        var taskB = await taskService.CreateTaskAsync(new TaskCreateRequest { Title = "任务 B" });

        await timerService.StartTaskAsync(taskA.Id);
        clock.Advance(TimeSpan.FromMinutes(30));
        await timerService.SwitchTaskAsync(taskB.Id);
        clock.Advance(TimeSpan.FromMinutes(15));
        await timerService.PauseTaskAsync(taskB.Id);
        clock.Advance(TimeSpan.FromMinutes(5));
        await timerService.ResumeTaskAsync(taskB.Id);
        clock.Advance(TimeSpan.FromMinutes(10));
        await timerService.CompleteTaskAsync(taskB.Id);

        var tasks = db.Context.TaskItems.OrderBy(item => item.Title).ToList();
        tasks.Single(item => item.Id == taskA.Id).Status.Should().Be(TaskStatus.Paused);
        tasks.Single(item => item.Id == taskB.Id).Status.Should().Be(TaskStatus.Completed);

        var taskAEntries = db.Context.TimeEntriesSet.Where(entry => entry.TaskItemId == taskA.Id).ToList();
        var taskBEntries = db.Context.TimeEntriesSet.Where(entry => entry.TaskItemId == taskB.Id).AsEnumerable().OrderBy(entry => entry.StartAt).ToList();

        taskAEntries.Should().HaveCount(1);
        taskAEntries[0].DurationSeconds.Should().Be(1800);
        taskBEntries.Should().HaveCount(2);
        taskBEntries.Sum(entry => entry.DurationSeconds).Should().Be(1500);
        db.Context.TimeEntriesSet.Count(entry => entry.EndAt == null).Should().Be(0);
    }

    [Fact]
    public async Task StartTaskAsync_ShouldIgnoreDuplicateStartAndRejectCompletedTask()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 29, 13, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();
        var taskService = new TaskService(db.Context, clock);
        var timerService = new TimerService(db.Context, clock);

        var task = await taskService.CreateTaskAsync(new TaskCreateRequest { Title = "重复开始任务" });

        await timerService.StartTaskAsync(task.Id);
        await timerService.StartTaskAsync(task.Id);
        db.Context.TimeEntriesSet.Count(entry => entry.TaskItemId == task.Id).Should().Be(1);

        clock.Advance(TimeSpan.FromMinutes(3));
        await timerService.CompleteTaskAsync(task.Id);

        var action = async () => await timerService.StartTaskAsync(task.Id);
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SubTaskFlow_ShouldTrackDurationsAndRequireCompletionBeforeParent()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 29, 14, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();
        var taskService = new TaskService(db.Context, clock);
        var timerService = new TimerService(db.Context, clock);
        var statisticsService = new StatisticsService(db.Context, clock);

        var task = await taskService.CreateTaskAsync(new TaskCreateRequest { Title = "父任务" });
        var subTaskA = await taskService.CreateSubTaskAsync(task.Id, new SubTaskCreateRequest { Title = "调研" });
        var subTaskB = await taskService.CreateSubTaskAsync(task.Id, new SubTaskCreateRequest { Title = "实现" });

        await timerService.StartSubTaskAsync(task.Id, subTaskA.Id);
        clock.Advance(TimeSpan.FromMinutes(20));
        await timerService.StartSubTaskAsync(task.Id, subTaskB.Id);
        clock.Advance(TimeSpan.FromMinutes(10));
        await timerService.CompleteSubTaskAsync(subTaskB.Id);

        db.Context.SubTaskItems.Single(item => item.Id == subTaskA.Id).Status.Should().Be(TaskStatus.Paused);
        db.Context.SubTaskItems.Single(item => item.Id == subTaskB.Id).Status.Should().Be(TaskStatus.Completed);
        db.Context.SubTaskItems.Single(item => item.Id == subTaskB.Id).CompletedAt.Should().Be(clock.Now);
        db.Context.TaskItems.Single(item => item.Id == task.Id).Status.Should().Be(TaskStatus.Paused);

        (await statisticsService.GetSubTaskDurationAsync(subTaskA.Id)).Should().Be(TimeSpan.FromMinutes(20));
        (await statisticsService.GetSubTaskDurationAsync(subTaskB.Id)).Should().Be(TimeSpan.FromMinutes(10));
        (await statisticsService.GetTaskDurationAsync(task.Id)).Should().Be(TimeSpan.FromMinutes(30));

        var completeParentWithOpenSubTask = async () => await timerService.CompleteTaskAsync(task.Id);
        await completeParentWithOpenSubTask.Should().ThrowAsync<InvalidOperationException>();

        await timerService.CompleteSubTaskAsync(subTaskA.Id);
        await timerService.CompleteTaskAsync(task.Id);

        db.Context.TaskItems.Single(item => item.Id == task.Id).Status.Should().Be(TaskStatus.Completed);
    }
}
