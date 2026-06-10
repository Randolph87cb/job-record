using FluentAssertions;
using JobRecord.Core.Dtos;
using JobRecord.Core.Enums;
using JobRecord.Core.Services;
using JobRecord.Tests.Common;

namespace JobRecord.Tests.Core;

public sealed class TaskServiceTests
{
    [Fact]
    public async Task CreateTaskAsync_ShouldCreateTaskAndRejectEmptyTitle()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 29, 9, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();
        var service = new TaskService(db.Context, clock);

        var created = await service.CreateTaskAsync(new TaskCreateRequest
        {
            Title = "整理月报",
            Priority = TaskPriority.P1
        });

        created.Title.Should().Be("整理月报");
        created.Priority.Should().Be(TaskPriority.P1);
        created.Status.Should().Be(TaskStatus.Pending);

        var action = async () => await service.CreateTaskAsync(new TaskCreateRequest { Title = "   " });
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RenameTaskAsync_ShouldTrimAndPersistNewTitle()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 29, 9, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();
        var service = new TaskService(db.Context, clock);

        var task = await service.CreateTaskAsync(new TaskCreateRequest { Title = "原任务" });
        clock.Advance(TimeSpan.FromMinutes(5));

        var renamed = await service.RenameTaskAsync(task.Id, "  新任务标题  ");

        renamed.Title.Should().Be("新任务标题");
        renamed.UpdatedAt.Should().Be(clock.Now);
        db.Context.TaskItems.Single(item => item.Id == task.Id).Title.Should().Be("新任务标题");
    }

    [Fact]
    public async Task GetTaskListAsync_ShouldPutLatestPausedTaskFirst()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 29, 10, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();
        var taskService = new TaskService(db.Context, clock);
        var timerService = new TimerService(db.Context, clock);

        var firstTask = await taskService.CreateTaskAsync(new TaskCreateRequest { Title = "先暂停任务", Priority = TaskPriority.P2 });
        var secondTask = await taskService.CreateTaskAsync(new TaskCreateRequest { Title = "后暂停任务", Priority = TaskPriority.P2 });

        await timerService.StartTaskAsync(firstTask.Id);
        clock.Advance(TimeSpan.FromMinutes(3));
        await timerService.PauseTaskAsync(firstTask.Id);

        clock.Advance(TimeSpan.FromMinutes(1));
        await timerService.StartTaskAsync(secondTask.Id);
        clock.Advance(TimeSpan.FromMinutes(2));
        await timerService.PauseTaskAsync(secondTask.Id);

        var tasks = await taskService.GetTaskListAsync();

        tasks.Take(2).Select(task => task.Id).Should().ContainInOrder(secondTask.Id, firstTask.Id);
    }

    [Fact]
    public async Task ArchiveTaskAsync_ShouldArchiveTasksWithEntriesAndDeleteTasksWithoutEntries()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 29, 10, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();
        var taskService = new TaskService(db.Context, clock);
        var timerService = new TimerService(db.Context, clock);

        var withEntries = await taskService.CreateTaskAsync(new TaskCreateRequest { Title = "有记录任务" });
        var withoutEntries = await taskService.CreateTaskAsync(new TaskCreateRequest { Title = "无记录任务" });

        await timerService.StartTaskAsync(withEntries.Id);
        clock.Advance(TimeSpan.FromMinutes(10));
        await timerService.PauseTaskAsync(withEntries.Id);

        await taskService.ArchiveTaskAsync(withEntries.Id);
        await taskService.ArchiveTaskAsync(withoutEntries.Id);

        db.Context.TaskItems.Single(item => item.Id == withEntries.Id).IsArchived.Should().BeTrue();
        db.Context.TaskItems.Any(item => item.Id == withoutEntries.Id).Should().BeFalse();
    }
}
