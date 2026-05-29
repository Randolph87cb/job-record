using FluentAssertions;
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
}
