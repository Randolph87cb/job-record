using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using JobRecord.App.Layout;
using JobRecord.App.Preview;
using JobRecord.App.Services;
using JobRecord.App.ViewModels;

namespace JobRecord.App;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly TrayIconService _trayIconService;
    private readonly IUserNotificationService _notificationService;
    private readonly PreviewLaunchOptions _previewOptions;
    private readonly DispatcherTimer _refreshTimer;
    private DateTimeOffset _lastInteractionAt = DateTimeOffset.Now;
    private bool _allowClose;
    private bool _ignoreNextHeaderClick;
    private bool _trayAvailable;

    public MainWindow(
        ShellViewModel viewModel,
        TrayIconService trayIconService,
        IUserNotificationService notificationService,
        PreviewLaunchOptions previewOptions)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _trayIconService = trayIconService;
        _notificationService = notificationService;
        _previewOptions = previewOptions;
        DataContext = _viewModel;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimerOnTick;

        if (_previewOptions.IsEnabled)
        {
            _trayAvailable = false;
            return;
        }

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
        if (_previewOptions.IsEnabled)
        {
            _viewModel.SetCompact(_previewOptions.IsCompact);
        }

        ApplyWindowMetrics();
        ApplyPlacement(WindowDockLayoutCalculator.GetPlacementForMode(
            SystemParameters.WorkArea,
            GetWindowSize(),
            _viewModel.CurrentDockMode,
            _viewModel.MarginTop,
            _viewModel.MarginSide,
            _viewModel.WindowLeft,
            _viewModel.WindowTop));

        if (!_viewModel.IsBarVisible)
        {
            Hide();
        }

        if (_previewOptions.ShouldCaptureScreenshot)
        {
            await CapturePreviewScreenshotAndExitAsync();
            return;
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
                await SnapAndPersistAfterDragAsync();
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
        ApplyWindowMetrics();
        ApplyPlacement(WindowDockLayoutCalculator.GetPlacementForMode(
            SystemParameters.WorkArea,
            GetWindowSize(),
            _viewModel.CurrentDockMode,
            _viewModel.MarginTop,
            _viewModel.MarginSide,
            Left,
            Top));
    }

    private void OnMouseEnteredWindow(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _lastInteractionAt = DateTimeOffset.Now;
        if (_viewModel.IsTopDocked)
        {
            _viewModel.SetCompact(false);
            ApplyWindowMetrics();
            ApplyPlacement(WindowDockLayoutCalculator.GetPlacementForMode(
                SystemParameters.WorkArea,
                GetWindowSize(),
                _viewModel.CurrentDockMode,
                _viewModel.MarginTop,
                _viewModel.MarginSide,
                Left,
                Top));
        }
    }

    private void OnMouseLeftWindow(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _lastInteractionAt = DateTimeOffset.Now;
    }

    private async void RefreshTimerOnTick(object? sender, EventArgs e)
    {
        if (Keyboard.FocusedElement is not System.Windows.Controls.TextBox)
        {
            await _viewModel.RefreshAsync();
        }

        var shouldCompact = !_viewModel.IsExpanded &&
            _viewModel.HasCurrentTask &&
            _viewModel.IsTopDocked &&
            DateTimeOffset.Now - _lastInteractionAt > TimeSpan.FromSeconds(8);

        _viewModel.SetCompact(shouldCompact);
        ApplyWindowMetrics();
        ApplyPlacement(WindowDockLayoutCalculator.GetPlacementForMode(
            SystemParameters.WorkArea,
            GetWindowSize(),
            _viewModel.CurrentDockMode,
            _viewModel.MarginTop,
            _viewModel.MarginSide,
            Left,
            Top));
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
            ApplyWindowMetrics();
            ApplyPlacement(WindowDockLayoutCalculator.GetPlacementForMode(
                SystemParameters.WorkArea,
                GetWindowSize(),
                _viewModel.CurrentDockMode,
                _viewModel.MarginTop,
                _viewModel.MarginSide,
                _viewModel.WindowLeft,
                _viewModel.WindowTop));
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

    private void ApplyWindowMetrics()
    {
        MinWidth = _viewModel.CurrentMinWindowWidth;
        Width = _viewModel.CurrentWindowWidth;
        UpdateLayout();
    }

    private System.Windows.Size GetWindowSize()
    {
        var height = ActualHeight > 0 ? ActualHeight : Math.Max(Height, MinHeight);
        return new System.Windows.Size(Width, height);
    }

    private void ApplyPlacement(WindowDockPlacement placement)
    {
        Left = placement.Left;
        Top = placement.Top;
    }

    private async Task SnapAndPersistAfterDragAsync()
    {
        var releasePlacement = WindowDockLayoutCalculator.ResolveDragRelease(
            SystemParameters.WorkArea,
            GetWindowSize(),
            Left,
            Top,
            _viewModel.MarginTop,
            _viewModel.MarginSide);

        await _viewModel.SaveWindowPlacementAsync(releasePlacement.DockMode, releasePlacement.Left, releasePlacement.Top);
        ApplyWindowMetrics();
        var finalPlacement = WindowDockLayoutCalculator.GetPlacementForMode(
            SystemParameters.WorkArea,
            GetWindowSize(),
            _viewModel.CurrentDockMode,
            _viewModel.MarginTop,
            _viewModel.MarginSide,
            releasePlacement.Left,
            releasePlacement.Top);

        ApplyPlacement(finalPlacement);
        await _viewModel.SaveWindowPlacementAsync(finalPlacement.DockMode, finalPlacement.Left, finalPlacement.Top);
    }

    private async Task CapturePreviewScreenshotAndExitAsync()
    {
        await Dispatcher.InvokeAsync(UpdateLayout, DispatcherPriority.Render);
        await Task.Delay(250);
        await Dispatcher.InvokeAsync(UpdateLayout, DispatcherPriority.Render);

        var screenshotPath = _previewOptions.ScreenshotPath!;
        var directoryPath = Path.GetDirectoryName(screenshotPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var width = Math.Max(1, (int)Math.Ceiling(CaptureRoot.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(CaptureRoot.ActualHeight));
        var renderBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        renderBitmap.Render(CaptureRoot);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

        await using var fileStream = File.Create(screenshotPath);
        encoder.Save(fileStream);

        _allowClose = true;
        Close();
    }
}
