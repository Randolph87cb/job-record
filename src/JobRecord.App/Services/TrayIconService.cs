using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace JobRecord.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public TrayIconService()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "工作时间记录",
            Visible = true
        };
    }

    public event EventHandler? ToggleVisibilityRequested;
    public event EventHandler? PauseResumeRequested;
    public event EventHandler? CompleteRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示/隐藏", null, (_, _) => ToggleVisibilityRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("暂停/继续", null, (_, _) => PauseResumeRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("完成当前任务", null, (_, _) => CompleteRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ToggleVisibilityRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
