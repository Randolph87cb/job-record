using System.Collections.ObjectModel;
using System.Windows.Input;
using JobRecord.App.Commands;
using JobRecord.App.Services;
using JobRecord.App.Styling;
using JobRecord.Core.Abstractions;
using JobRecord.Core.Dtos;
using JobRecord.Core.Enums;
using JobRecord.Core.Models;
using Brush = System.Windows.Media.Brush;

namespace JobRecord.App.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private readonly ITaskService _taskService;
    private readonly ITimerService _timerService;
    private readonly IStatisticsService _statisticsService;
    private readonly ISettingsService _settingsService;
    private readonly IUserNotificationService _notificationService;
    private readonly List<TaskListItemViewModel> _allTasks = [];

    private TaskItem? _displayTask;
    private string _currentTaskTitle = "无当前任务";
    private string _currentDurationText = "00:00:00";
    private string _currentPriorityText = string.Empty;
    private string _statusText = "空闲";
    private string _todaySummaryText = "今日 0 分钟";
    private string _newTaskTitle = string.Empty;
    private TaskPriority _newTaskPriority = TaskPriority.P2;
    private TaskListFilterOption _selectedTaskFilter = TaskListFilterOption.All;
    private bool _isExpanded;
    private bool _isCompact;
    private bool _isBarVisible = true;
    private double _barWidth = 460;
    private double _barHeight = 34;
    private double _marginTop = 6;
    private double _marginSide = 12;
    private double? _windowLeft;
    private double? _windowTop;
    private DockMode _currentDockMode = DockMode.TopCenter;

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
        CreateTaskCommand = new AsyncRelayCommand(() => RunSafeAsync(() => CreateTaskInternalAsync(false), "创建任务失败。"), () => !string.IsNullOrWhiteSpace(NewTaskTitle));
        CreateTaskAndStartCommand = new AsyncRelayCommand(() => RunSafeAsync(CreateTaskAndStartInternalAsync, "创建任务失败。"), () => !string.IsNullOrWhiteSpace(NewTaskTitle));
        PrimaryTaskActionCommand = new AsyncRelayCommand<TaskListItemViewModel>(item => RunSafeAsync(() => ExecutePrimaryTaskActionInternalAsync(item), "执行任务操作失败。"), item => item.CanPrimaryAction);
        CompleteTaskCommand = new AsyncRelayCommand<TaskListItemViewModel>(item => RunSafeAsync(() => CompleteTaskInternalAsync(item), "完成任务失败。"), item => item.CanComplete);
        BeginEditTaskCommand = new AsyncRelayCommand<TaskListItemViewModel>(item => RunSafeAsync(() => BeginEditTaskInternalAsync(item), "进入编辑失败。"), item => item is not null && !item.IsEditing);
        CancelEditTaskCommand = new AsyncRelayCommand<TaskListItemViewModel>(item => RunSafeAsync(() => CancelEditTaskInternalAsync(item), "取消编辑失败。"), item => item is not null && item.IsEditing);
        SaveTaskEditCommand = new AsyncRelayCommand<TaskListItemViewModel>(item => RunSafeAsync(() => SaveTaskEditInternalAsync(item), "保存任务名称失败。"), item => item is not null && item.IsEditing);
    }

    public ObservableCollection<TaskListItemViewModel> Tasks { get; }

    public ICommand ToggleExpandedCommand { get; }
    public ICommand ToggleWorkStateCommand { get; }
    public ICommand CompleteCurrentTaskCommand { get; }
    public ICommand CreateTaskCommand { get; }
    public ICommand CreateTaskAndStartCommand { get; }
    public ICommand PrimaryTaskActionCommand { get; }
    public ICommand CompleteTaskCommand { get; }
    public ICommand BeginEditTaskCommand { get; }
    public ICommand CancelEditTaskCommand { get; }
    public ICommand SaveTaskEditCommand { get; }

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

    public TaskListFilterOption SelectedTaskFilter
    {
        get => _selectedTaskFilter;
        set
        {
            if (SetProperty(ref _selectedTaskFilter, value))
            {
                ApplyTaskFilter();
            }
        }
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

    public DockMode CurrentDockMode
    {
        get => _currentDockMode;
        private set => SetProperty(ref _currentDockMode, value);
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

    public double MarginSide
    {
        get => _marginSide;
        private set => SetProperty(ref _marginSide, value);
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
    public bool IsTopDocked => CurrentDockMode is DockMode.TopCenter or DockMode.Floating;
    public bool IsLeftDocked => CurrentDockMode == DockMode.LeftEdge;
    public bool IsRightDocked => CurrentDockMode == DockMode.RightEdge;
    public bool IsFloating => CurrentDockMode == DockMode.Floating;
    public double SideBarWidth => 56;
    public double SideTimerPillWidth => 76;
    public double SideTimerOverlap => 54;
    public double SideDrawerWidth => 320;
    public double SideDrawerGap => 10;
    public double SideCollapsedWidth => SideBarWidth + SideTimerPillWidth - SideTimerOverlap;
    public double LeftTimerOffset => SideBarWidth - SideTimerOverlap;
    public double LeftDrawerOffset => SideCollapsedWidth + SideDrawerGap;
    public double RightTimerOffset => IsExpanded ? SideDrawerWidth + SideDrawerGap : 0;
    public double RightSidebarOffset => RightTimerOffset + SideCollapsedWidth - SideBarWidth;
    public double CurrentWindowWidth => IsTopDocked
        ? (IsCompact ? CompactBarWidth : BarWidth)
        : SideCollapsedWidth + (IsExpanded ? SideDrawerWidth + SideDrawerGap : 0);
    public double CurrentMinWindowWidth => IsTopDocked ? 280 : SideCollapsedWidth;
    public double TaskListMaxHeight => IsTopDocked ? 320 : 280;
    public string WorkActionText => IsRunning ? "暂停" : "继续";
    public Brush CurrentPriorityBackground => HasCurrentTask ? UiPalette.GetPriorityBackground(_displayTask!.Priority) : UiPalette.PendingBackground;
    public Brush CurrentPriorityForeground => HasCurrentTask ? UiPalette.GetPriorityForeground(_displayTask!.Priority) : UiPalette.PendingForeground;
    public Brush CurrentPrioritySoftBackground => HasCurrentTask ? UiPalette.GetPrioritySoftBackground(_displayTask!.Priority) : UiPalette.PendingBackground;
    public Brush CurrentStatusBackground => HasCurrentTask ? UiPalette.GetStatusBackground(_displayTask!.Status) : UiPalette.PendingBackground;
    public Brush CurrentStatusForeground => HasCurrentTask ? UiPalette.GetStatusForeground(_displayTask!.Status) : UiPalette.PendingForeground;
    public Brush CurrentPanelBorderBrush => HasCurrentTask ? UiPalette.BorderStrong : UiPalette.BorderDefault;
    public Brush CurrentPanelBackground => HasCurrentTask ? UiPalette.SurfaceCurrent : UiPalette.SurfaceAccent;
    public string SideTaskVerticalText => BuildVerticalText(HasCurrentTask ? GetSideTaskTitle(CurrentTaskTitle) : "空闲");
    public string SideTimerText => HasCurrentTask ? CurrentDurationText : "待开始";
    public string SideToggleHintText => CurrentDockMode switch
    {
        DockMode.LeftEdge => IsExpanded ? "<" : ">",
        DockMode.RightEdge => IsExpanded ? ">" : "<",
        _ => string.Empty
    };

    public async Task InitializeAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        var runtimeState = await _settingsService.GetRuntimeStateAsync();

        CurrentDockMode = settings.DockMode;
        BarWidth = settings.BarWidth;
        BarHeight = settings.BarHeight;
        MarginTop = settings.MarginTop;
        MarginSide = settings.MarginSide;
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

        _allTasks.Clear();
        foreach (var task in tasks)
        {
            var duration = await _statisticsService.GetTaskDurationAsync(task.Id);
            _allTasks.Add(new TaskListItemViewModel
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
                IsCurrent = _displayTask?.Id == task.Id,
                EditableTitle = task.Title
            });
        }

        ApplyTaskFilter();
        RaiseStatePropertiesChanged();
        RaisePropertyChanged(nameof(CompactBarWidth));
        RaisePropertyChanged(nameof(CurrentWindowWidth));
        RaisePropertyChanged(nameof(CurrentMinWindowWidth));
    }

    public async Task SetExpandedAsync(bool value)
    {
        IsExpanded = value;
        await _settingsService.SetExpandedStateAsync(value);
        RaiseLayoutPropertiesChanged();
    }

    public void SetCompact(bool value)
    {
        if (SetProperty(ref _isCompact, value))
        {
            RaisePropertyChanged(nameof(CompactBarWidth));
            RaisePropertyChanged(nameof(CurrentWindowWidth));
        }
    }

    public async Task SetBarVisibleAsync(bool value)
    {
        IsBarVisible = value;
        await _settingsService.SetBarVisibilityAsync(value);
    }

    public async Task SaveWindowPlacementAsync(DockMode dockMode, double left, double top)
    {
        CurrentDockMode = dockMode;
        WindowLeft = left;
        WindowTop = top;
        await _settingsService.SaveWindowPlacementAsync(dockMode, left, top);
        RaiseLayoutPropertiesChanged();
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

    private async Task CreateTaskInternalAsync(bool startImmediately)
    {
        var task = await _taskService.CreateTaskAsync(new TaskCreateRequest
        {
            Title = NewTaskTitle,
            Priority = NewTaskPriority
        });

        if (startImmediately)
        {
            await _timerService.StartTaskAsync(task.Id);
        }

        NewTaskTitle = string.Empty;
        await SetExpandedAsync(true);
        await RefreshAsync();
    }

    private async Task CreateTaskAndStartInternalAsync()
    {
        await CreateTaskInternalAsync(true);
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

    private Task BeginEditTaskInternalAsync(TaskListItemViewModel item)
    {
        foreach (var existingItem in _allTasks.Where(existingItem => existingItem.IsEditing && existingItem.Id != item.Id))
        {
            existingItem.CancelEdit();
        }

        item.BeginEdit();
        RaiseCommandStateChanged();
        return Task.CompletedTask;
    }

    private Task CancelEditTaskInternalAsync(TaskListItemViewModel item)
    {
        item.CancelEdit();
        RaiseCommandStateChanged();
        return Task.CompletedTask;
    }

    private async Task SaveTaskEditInternalAsync(TaskListItemViewModel item)
    {
        if (!item.CanSaveEdit)
        {
            return;
        }

        await _taskService.RenameTaskAsync(item.Id, item.EditableTitle);
        await RefreshAsync();
    }

    private void ApplyTaskFilter()
    {
        Tasks.Clear();
        foreach (var task in _allTasks.Where(MatchesSelectedTaskFilter))
        {
            Tasks.Add(task);
        }

        RaiseCommandStateChanged();
    }

    private bool MatchesSelectedTaskFilter(TaskListItemViewModel task)
        => SelectedTaskFilter switch
        {
            TaskListFilterOption.Pending => task.Status == TaskStatus.Pending,
            TaskListFilterOption.Running => task.Status == TaskStatus.Running,
            TaskListFilterOption.Paused => task.Status == TaskStatus.Paused,
            TaskListFilterOption.Completed => task.Status == TaskStatus.Completed,
            _ => true
        };

    private void RaiseStatePropertiesChanged()
    {
        RaisePropertyChanged(nameof(HasCurrentTask));
        RaisePropertyChanged(nameof(IsRunning));
        RaisePropertyChanged(nameof(IsPaused));
        RaisePropertyChanged(nameof(WorkActionText));
        RaisePropertyChanged(nameof(CurrentPriorityBackground));
        RaisePropertyChanged(nameof(CurrentPriorityForeground));
        RaisePropertyChanged(nameof(CurrentPrioritySoftBackground));
        RaisePropertyChanged(nameof(CurrentStatusBackground));
        RaisePropertyChanged(nameof(CurrentStatusForeground));
        RaisePropertyChanged(nameof(CurrentPanelBorderBrush));
        RaisePropertyChanged(nameof(CurrentPanelBackground));
        RaisePropertyChanged(nameof(SideTaskVerticalText));
        RaisePropertyChanged(nameof(SideTimerText));
        RaiseCommandStateChanged();
        RaiseLayoutPropertiesChanged();
    }

    private void RaiseLayoutPropertiesChanged()
    {
        RaisePropertyChanged(nameof(CurrentDockMode));
        RaisePropertyChanged(nameof(IsTopDocked));
        RaisePropertyChanged(nameof(IsLeftDocked));
        RaisePropertyChanged(nameof(IsRightDocked));
        RaisePropertyChanged(nameof(IsFloating));
        RaisePropertyChanged(nameof(SideBarWidth));
        RaisePropertyChanged(nameof(SideTimerPillWidth));
        RaisePropertyChanged(nameof(SideTimerOverlap));
        RaisePropertyChanged(nameof(SideDrawerWidth));
        RaisePropertyChanged(nameof(SideDrawerGap));
        RaisePropertyChanged(nameof(SideCollapsedWidth));
        RaisePropertyChanged(nameof(LeftTimerOffset));
        RaisePropertyChanged(nameof(LeftDrawerOffset));
        RaisePropertyChanged(nameof(RightTimerOffset));
        RaisePropertyChanged(nameof(RightSidebarOffset));
        RaisePropertyChanged(nameof(CurrentWindowWidth));
        RaisePropertyChanged(nameof(CurrentMinWindowWidth));
        RaisePropertyChanged(nameof(TaskListMaxHeight));
        RaisePropertyChanged(nameof(SideToggleHintText));
    }

    private void RaiseCommandStateChanged()
    {
        (ToggleWorkStateCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CompleteCurrentTaskCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CreateTaskCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CreateTaskAndStartCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (PrimaryTaskActionCommand as AsyncRelayCommand<TaskListItemViewModel>)?.RaiseCanExecuteChanged();
        (CompleteTaskCommand as AsyncRelayCommand<TaskListItemViewModel>)?.RaiseCanExecuteChanged();
        (BeginEditTaskCommand as AsyncRelayCommand<TaskListItemViewModel>)?.RaiseCanExecuteChanged();
        (CancelEditTaskCommand as AsyncRelayCommand<TaskListItemViewModel>)?.RaiseCanExecuteChanged();
        (SaveTaskEditCommand as AsyncRelayCommand<TaskListItemViewModel>)?.RaiseCanExecuteChanged();
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

    private static string GetSideTaskTitle(string title)
    {
        const int maxLength = 4;
        var chars = title.Trim().Take(maxLength).ToArray();
        return chars.Length == 0 ? "空" : new string(chars);
    }

    private static string BuildVerticalText(string value)
        => string.Join(Environment.NewLine, value.Trim().ToCharArray());
}
