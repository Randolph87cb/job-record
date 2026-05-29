using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using JobRecord.App.Services;
using JobRecord.App.ViewModels;

namespace JobRecord.App;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly TrayIconService _trayIconService;
    private readonly IUserNotificationService _notificationService;
    private readonly DispatcherTimer _refreshTimer;
    private DateTimeOffset _lastInteractionAt = DateTimeOffset.Now;
    private bool _allowClose;
    private bool _ignoreNextHeaderClick;
    private bool _trayAvailable;

    public MainWindow(
        ShellViewModel viewModel,
        TrayIconService trayIconService,
        IUserNotificationService notificationService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _trayIconService = trayIconService;
        _notificationService = notificationService;
        DataContext = _viewModel;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimerOnTick;

        try
        {
            _trayIconService.Initialize();
            _trayIconService.ToggleVisibilityRequested += OnToggleVisibilityRequested;
            _trayIconService.PauseResumeRequested += (_, _) => Dispatcher.Invoke(() => _viewModel.ToggleWorkStateCommand.Execute(null));
            _trayIconService.CompleteRequested += (_, _) => Dispatcher.Invoke(() => _viewModel.CompleteCurrentTaskCommand.Execute(null));
            _trayIconService.ExitRequested += (_, _) =>
            {
                _allowClose = true;
                Close();
            };
            _trayAvailable = true;
        }
        catch (Exception ex)
        {
            _trayAvailable = false;
            _notificationService.ShowError("工作时间记录", $"托盘初始化失败，将继续运行但不提供托盘菜单。\n\n{ex.Message}");
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        Width = _viewModel.BarWidth;
        Top = _viewModel.WindowTop ?? _viewModel.MarginTop;
        Left = _viewModel.WindowLeft ?? GetCenteredLeft(Width);

        if (!_viewModel.IsBarVisible)
        {
            Hide();
        }

        _refreshTimer.Start();
    }

    private async void OnDragHandleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _lastInteractionAt = DateTimeOffset.Now;

        try
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
                await _viewModel.SaveWindowPlacementAsync(Left, Top);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void OnHeaderContentMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _lastInteractionAt = DateTimeOffset.Now;

        if (e.ClickCount == 2)
        {
            _ignoreNextHeaderClick = true;
            _viewModel.ToggleWorkStateCommand.Execute(null);
        }
    }

    private async void OnHeaderContentMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _lastInteractionAt = DateTimeOffset.Now;

        if (_ignoreNextHeaderClick)
        {
            _ignoreNextHeaderClick = false;
            return;
        }

        await _viewModel.SetExpandedAsync(!_viewModel.IsExpanded);
    }

    private void OnMouseEnteredWindow(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _lastInteractionAt = DateTimeOffset.Now;
        _viewModel.SetCompact(false);
        Width = _viewModel.BarWidth;
        if (!_viewModel.WindowLeft.HasValue)
        {
            Left = GetCenteredLeft(Width);
        }
    }

    private void OnMouseLeftWindow(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _lastInteractionAt = DateTimeOffset.Now;
    }

    private async void RefreshTimerOnTick(object? sender, EventArgs e)
    {
        await _viewModel.RefreshAsync();

        var shouldCompact = !_viewModel.IsExpanded &&
            _viewModel.HasCurrentTask &&
            DateTimeOffset.Now - _lastInteractionAt > TimeSpan.FromSeconds(8);

        _viewModel.SetCompact(shouldCompact);
        Width = shouldCompact ? _viewModel.CompactBarWidth : _viewModel.BarWidth;

        if (!_viewModel.WindowLeft.HasValue)
        {
            Left = GetCenteredLeft(Width);
        }
    }

    private async void OnToggleVisibilityRequested(object? sender, EventArgs e)
    {
        if (IsVisible)
        {
            Hide();
            await _viewModel.SetBarVisibleAsync(false);
        }
        else
        {
            Show();
            Activate();
            await _viewModel.SetBarVisibleAsync(true);
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || !_trayAvailable)
        {
            _refreshTimer.Stop();
            if (_trayAvailable)
            {
                _trayIconService.Dispose();
            }

            return;
        }

        e.Cancel = true;
        Hide();
        _ = _viewModel.SetBarVisibleAsync(false);
    }

    private static double GetCenteredLeft(double width)
        => (SystemParameters.WorkArea.Width - width) / 2;
}
