using FluentAssertions;
using JobRecord.App.Layout;
using JobRecord.App.Services;
using JobRecord.App.ViewModels;
using JobRecord.Core.Dtos;
using JobRecord.Core.Enums;
using JobRecord.Core.Services;
using JobRecord.Tests.Common;

namespace JobRecord.Tests.App;

public sealed class ShellViewModelTests
{
    [Fact]
    public async Task RefreshAsync_ShouldMapRunningTaskIntoDisplayState()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 29, 9, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();

        var taskService = new TaskService(db.Context, clock);
        var timerService = new TimerService(db.Context, clock);
        var settingsService = new SettingsService(db.Context, clock);
        var statisticsService = new StatisticsService(db.Context, clock);

        var task = await taskService.CreateTaskAsync(new TaskCreateRequest
        {
            Title = "界面任务",
            Priority = TaskPriority.P1
        });

        await timerService.StartTaskAsync(task.Id);
        clock.Advance(TimeSpan.FromMinutes(12));

        var viewModel = new ShellViewModel(taskService, timerService, statisticsService, settingsService, new SilentNotificationService());
        await viewModel.InitializeAsync();
        await viewModel.RefreshAsync();

        viewModel.HasCurrentTask.Should().BeTrue();
        viewModel.IsRunning.Should().BeTrue();
        viewModel.CurrentTaskTitle.Should().Be("界面任务");
        viewModel.CurrentPriorityText.Should().Be("P1");
        viewModel.CurrentDurationText.Should().Be("00:12:00");
        viewModel.Tasks.Should().ContainSingle(item => item.Title == "界面任务" && item.IsCurrent);
        viewModel.Tasks.Single().PrimaryActionText.Should().Be("暂停");

        viewModel.SetCompact(true);
        viewModel.IsCompact.Should().BeTrue();
        viewModel.CompactBarWidth.Should().BeLessThan(viewModel.BarWidth);
    }

    [Fact]
    public async Task SaveWindowPlacementAsync_ShouldSwitchIntoLeftDockState()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 29, 9, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();

        var taskService = new TaskService(db.Context, clock);
        var timerService = new TimerService(db.Context, clock);
        var settingsService = new SettingsService(db.Context, clock);
        var statisticsService = new StatisticsService(db.Context, clock);

        var task = await taskService.CreateTaskAsync(new TaskCreateRequest
        {
            Title = "侧边任务显示",
            Priority = TaskPriority.P2
        });

        await timerService.StartTaskAsync(task.Id);
        clock.Advance(TimeSpan.FromMinutes(3));

        var viewModel = new ShellViewModel(taskService, timerService, statisticsService, settingsService, new SilentNotificationService());
        await viewModel.InitializeAsync();
        await viewModel.SaveWindowPlacementAsync(DockMode.LeftEdge, 12, 120);
        await viewModel.RefreshAsync();

        viewModel.IsLeftDocked.Should().BeTrue();
        viewModel.IsTopDocked.Should().BeFalse();
        viewModel.CurrentDockMode.Should().Be(DockMode.LeftEdge);
        viewModel.CurrentWindowWidth.Should().Be(viewModel.SideCollapsedWidth);
        viewModel.SideTaskVerticalText.Should().Contain(Environment.NewLine);
        viewModel.SideTimerText.Should().Be("00:03:00");

        await viewModel.SetExpandedAsync(true);

        viewModel.CurrentWindowWidth.Should().Be(viewModel.SideCollapsedWidth + viewModel.SideDrawerWidth + viewModel.SideDrawerGap);
        viewModel.SideToggleHintText.Should().Be("<");
    }

    [Fact]
    public async Task SelectedTaskFilter_ShouldOnlyExposeMatchingTasks()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 5, 29, 9, 0, 0, TimeSpan.Zero));
        using var db = TestDbContextFactory.CreateInMemory();

        var taskService = new TaskService(db.Context, clock);
        var timerService = new TimerService(db.Context, clock);
        var settingsService = new SettingsService(db.Context, clock);
        var statisticsService = new StatisticsService(db.Context, clock);

        var pausedTask = await taskService.CreateTaskAsync(new TaskCreateRequest { Title = "暂停任务", Priority = TaskPriority.P1 });
        var pendingTask = await taskService.CreateTaskAsync(new TaskCreateRequest { Title = "待开始任务", Priority = TaskPriority.P2 });
        var completedTask = await taskService.CreateTaskAsync(new TaskCreateRequest { Title = "已完成任务", Priority = TaskPriority.P3 });

        await timerService.StartTaskAsync(pausedTask.Id);
        clock.Advance(TimeSpan.FromMinutes(5));
        await timerService.PauseTaskAsync(pausedTask.Id);

        await timerService.StartTaskAsync(completedTask.Id);
        clock.Advance(TimeSpan.FromMinutes(2));
        await timerService.CompleteTaskAsync(completedTask.Id);

        var viewModel = new ShellViewModel(taskService, timerService, statisticsService, settingsService, new SilentNotificationService());
        await viewModel.InitializeAsync();

        viewModel.SelectedTaskFilter = TaskListFilterOption.Paused;
        viewModel.Tasks.Select(item => item.Title).Should().Equal("暂停任务");

        viewModel.SelectedTaskFilter = TaskListFilterOption.Pending;
        viewModel.Tasks.Select(item => item.Title).Should().Equal("待开始任务");

        viewModel.SelectedTaskFilter = TaskListFilterOption.All;
        viewModel.Tasks.Should().HaveCount(3);
    }
}
