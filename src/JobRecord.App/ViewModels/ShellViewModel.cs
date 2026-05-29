using System.Collections.ObjectModel;
using System.Windows.Input;
using JobRecord.App.Commands;
using JobRecord.App.Services;
using JobRecord.Core.Abstractions;
using JobRecord.Core.Dtos;
using JobRecord.Core.Enums;
using JobRecord.Core.Models;

namespace JobRecord.App.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private readonly ITaskService _taskService;
    private readonly ITimerService _timerService;
    private readonly IStatisticsService _statisticsService;
    private readonly ISettingsService _settingsService;
    private readonly IUserNotificationService _notificationService;

    private TaskItem? _displayTask;
    private string _currentTaskTitle = "无当前任务";
    private string _currentDurationText = "00:00:00";
    private string _currentPriorityText = string.Empty;
    private string _statusText = "空闲";
    private string _todaySummaryText = "今日 0 分钟";
    private string _newTaskTitle = string.Empty;
    private TaskPriority _newTaskPriority = TaskPriority.P2;
    private bool _isExpanded;
    private bool _isCompact;
    private bool _isBarVisible = true;
    private double _barWidth = 460;
    private double _barHeight = 34;
    private double _marginTop = 6;
    private double? _windowLeft;
    private double? _windowTop;

    public ShellViewModel(
        ITaskService taskService,
        ITimerService timerService,
        IStatisticsService statisticsService,
        ISettingsService settingsService,
        IUserNotificationService notificationService)
    {
        _taskService = taskService;
        _timerService = timerService;
        _statisticsService = statisticsService;
        _settingsService = settingsService;
        _notificationService = notificationService;

        Tasks = [];
        ToggleExpandedCommand = new AsyncRelayCommand(() => RunSafeAsync(ToggleExpandedInternalAsync, "切换展开状态失败。"));
        ToggleWorkStateCommand = new AsyncRelayCommand(() => RunSafeAsync(ToggleWorkStateInternalAsync, "更新任务状态失败。"), () => HasCurrentTask);
        CompleteCurrentTaskCommand = new AsyncRelayCommand(() => RunSafeAsync(CompleteCurrentTaskInternalAsync, "完成任务失败。"), () => HasCurrentTask);
        CreateTaskAndStartCommand = new AsyncRelayCommand(() => RunSafeAsync(CreateTaskAndStartInternalAsync, "创建任务失败。"), () => !string.IsNullOrWhiteSpace(NewTaskTitle));
        PrimaryTaskActionCommand = new AsyncRelayCommand<TaskListItemViewModel>(item => RunSafeAsync(() => ExecutePrimaryTaskActionInternalAsync(item), "执行任务操作失败。"), item => item.CanPrimaryAction);
        CompleteTaskCommand = new AsyncRelayCommand<TaskListItemViewModel>(item => RunSafeAsync(() => CompleteTaskInternalAsync(item), "完成任务失败。"), item => item.CanComplete);
    }

    public ObservableCollection<TaskListItemViewModel> Tasks { get; }

    public ICommand ToggleExpandedCommand { get; }
    public ICommand ToggleWorkStateCommand { get; }
    public ICommand CompleteCurrentTaskCommand { get; }
    public ICommand CreateTaskAndStartCommand { get; }
    public ICommand PrimaryTaskActionCommand { get; }
    public ICommand CompleteTaskCommand { get; }

    public string CurrentTaskTitle
    {
        get => _currentTaskTitle;
        private set => SetProperty(ref _currentTaskTitle, value);
    }

    public string CurrentDurationText
    {
        get => _currentDurationText;
        private set => SetProperty(ref _currentDurationText, value);
    }

    public string CurrentPriorityText
    {
        get => _currentPriorityText;
        private set => SetProperty(ref _currentPriorityText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string TodaySummaryText
    {
        get => _todaySummaryText;
        private set => SetProperty(ref _todaySummaryText, value);
    }

    public string NewTaskTitle
    {
        get => _newTaskTitle;
        set
        {
            if (SetProperty(ref _newTaskTitle, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public TaskPriority NewTaskPriority
    {
        get => _newTaskPriority;
        set => SetProperty(ref _newTaskPriority, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        private set => SetProperty(ref _isExpanded, value);
    }

    public bool IsCompact
    {
        get => _isCompact;
        private set => SetProperty(ref _isCompact, value);
    }

    public bool IsBarVisible
    {
        get => _isBarVisible;
        private set => SetProperty(ref _isBarVisible, value);
    }

    public double BarWidth
    {
        get => _barWidth;
        private set => SetProperty(ref _barWidth, value);
    }

    public double CompactBarWidth => Math.Max(280, BarWidth - 160);

    public double BarHeight
    {
        get => _barHeight;
        private set => SetProperty(ref _barHeight, value);
    }

    public double MarginTop
    {
        get => _marginTop;
        private set => SetProperty(ref _marginTop, value);
    }

    public double? WindowLeft
    {
        get => _windowLeft;
        private set => SetProperty(ref _windowLeft, value);
    }

    public double? WindowTop
    {
        get => _windowTop;
        private set => SetProperty(ref _windowTop, value);
    }

    public bool HasCurrentTask => _displayTask is not null;
    public bool IsRunning => _displayTask?.Status == TaskStatus.Running;
    public bool IsPaused => _displayTask?.Status == TaskStatus.Paused;
    public string WorkActionText => IsRunning ? "暂停" : "继续";

    public async Task InitializeAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        var runtimeState = await _settingsService.GetRuntimeStateAsync();

        BarWidth = settings.BarWidth;
        BarHeight = settings.BarHeight;
        MarginTop = settings.MarginTop;
        WindowLeft = settings.WindowLeft;
        WindowTop = settings.WindowTop;
        IsBarVisible = runtimeState.IsBarVisible;
        IsExpanded = runtimeState.IsExpanded;

        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        var tasks = await _taskService.GetTaskListAsync();
        var currentTask = tasks.FirstOrDefault(task => task.Status == TaskStatus.Running)
            ?? tasks.FirstOrDefault(task => task.Status == TaskStatus.Paused);

        _displayTask = currentTask;

        if (_displayTask is null)
        {
            CurrentTaskTitle = "无当前任务";
            CurrentDurationText = "00:00:00";
            CurrentPriorityText = string.Empty;
            StatusText = "空闲";
        }
        else
        {
            CurrentTaskTitle = _displayTask.Title;
            CurrentPriorityText = _displayTask.Priority.ToString();
            CurrentDurationText = FormatDuration(_displayTask.Status == TaskStatus.Running
                ? await _statisticsService.GetCurrentTaskDisplayDurationAsync(_displayTask.Id)
                : await _statisticsService.GetTaskDurationAsync(_displayTask.Id));
            StatusText = _displayTask.Status == TaskStatus.Running ? "进行中" : "已暂停";
        }

        var todaySummary = await _statisticsService.GetTodaySummaryAsync();
        TodaySummaryText = $"今日 {Math.Round(todaySummary.TotalDuration.TotalMinutes, 0)} 分钟 / 完成 {todaySummary.CompletedTaskCount}";

        Tasks.Clear();
        foreach (var task in tasks)
        {
            var duration = await _statisticsService.GetTaskDurationAsync(task.Id);
            Tasks.Add(new TaskListItemViewModel
            {
                Id = task.Id,
                Title = task.Title,
                Priority = task.Priority,
                Status = task.Status,
                StatusText = task.Status switch
                {
                    TaskStatus.Running => "进行中",
                    TaskStatus.Paused => "已暂停",
                    TaskStatus.Completed => "已完成",
                    _ => "待开始"
                },
                DurationText = FormatDuration(duration),
                EstimateText = task.EstimateMinutes.HasValue ? $"预估 {task.EstimateMinutes} 分钟" : "无预估",
                IsCurrent = _displayTask?.Id == task.Id
            });
        }

        RaiseStatePropertiesChanged();
        RaisePropertyChanged(nameof(CompactBarWidth));
    }

    public async Task SetExpandedAsync(bool value)
    {
        IsExpanded = value;
        await _settingsService.SetExpandedStateAsync(value);
    }

    public void SetCompact(bool value)
    {
        if (SetProperty(ref _isCompact, value))
        {
            RaisePropertyChanged(nameof(CompactBarWidth));
        }
    }

    public async Task SetBarVisibleAsync(bool value)
    {
        IsBarVisible = value;
        await _settingsService.SetBarVisibilityAsync(value);
    }

    public async Task SaveWindowPlacementAsync(double left, double top)
    {
        WindowLeft = left;
        WindowTop = top;
        await _settingsService.SaveWindowPlacementAsync(left, top);
    }

    private Task ToggleExpandedInternalAsync() => SetExpandedAsync(!IsExpanded);

    private async Task ToggleWorkStateInternalAsync()
    {
        if (_displayTask is null)
        {
            return;
        }

        if (_displayTask.Status == TaskStatus.Running)
        {
            await _timerService.PauseTaskAsync(_displayTask.Id);
        }
        else if (_displayTask.Status == TaskStatus.Paused)
        {
            await _timerService.ResumeTaskAsync(_displayTask.Id);
        }

        await RefreshAsync();
    }

    private async Task CompleteCurrentTaskInternalAsync()
    {
        if (_displayTask is null)
        {
            return;
        }

        await _timerService.CompleteTaskAsync(_displayTask.Id);
        await RefreshAsync();
    }

    private async Task CreateTaskAndStartInternalAsync()
    {
        var task = await _taskService.CreateTaskAsync(new TaskCreateRequest
        {
            Title = NewTaskTitle,
            Priority = NewTaskPriority
        });

        await _timerService.StartTaskAsync(task.Id);
        NewTaskTitle = string.Empty;
        await SetExpandedAsync(true);
        await RefreshAsync();
    }

    private async Task ExecutePrimaryTaskActionInternalAsync(TaskListItemViewModel item)
    {
        switch (item.Status)
        {
            case TaskStatus.Pending:
                await _timerService.StartTaskAsync(item.Id);
                break;
            case TaskStatus.Paused:
                await _timerService.ResumeTaskAsync(item.Id);
                break;
            case TaskStatus.Running:
                await _timerService.PauseTaskAsync(item.Id);
                break;
            case TaskStatus.Completed:
                return;
        }

        await RefreshAsync();
    }

    private async Task CompleteTaskInternalAsync(TaskListItemViewModel item)
    {
        if (!item.CanComplete)
        {
            return;
        }

        await _timerService.CompleteTaskAsync(item.Id);
        await RefreshAsync();
    }

    private void RaiseStatePropertiesChanged()
    {
        RaisePropertyChanged(nameof(HasCurrentTask));
        RaisePropertyChanged(nameof(IsRunning));
        RaisePropertyChanged(nameof(IsPaused));
        RaisePropertyChanged(nameof(WorkActionText));
        RaiseCommandStateChanged();
    }

    private void RaiseCommandStateChanged()
    {
        (ToggleWorkStateCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CompleteCurrentTaskCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CreateTaskAndStartCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (PrimaryTaskActionCommand as AsyncRelayCommand<TaskListItemViewModel>)?.RaiseCanExecuteChanged();
        (CompleteTaskCommand as AsyncRelayCommand<TaskListItemViewModel>)?.RaiseCanExecuteChanged();
    }

    private async Task RunSafeAsync(Func<Task> action, string userMessage)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _notificationService.ShowError("工作时间记录", $"{userMessage}\n\n{ex.Message}");
        }
    }

    private static string FormatDuration(TimeSpan duration)
        => duration < TimeSpan.Zero ? "00:00:00" : duration.ToString(@"hh\:mm\:ss");
}
