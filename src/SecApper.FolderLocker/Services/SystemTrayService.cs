using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SecApper.FolderLocker.Services;

public class SystemTrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private bool _isDisposed;

    public event Action? OpenRequested;
    public event Action? LockAllRequested;
    public event Action? PanicLockRequested;
    public event Action? ExitRequested;

    public SystemTrayService()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "SecApper Folder Locker",
            Visible = true
        };

        // Try load app icon
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
        if (File.Exists(iconPath))
        {
            try
            {
                _notifyIcon.Icon = new Icon(iconPath);
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Shield;
            }
        }
        else
        {
            _notifyIcon.Icon = SystemIcons.Shield;
        }

        var contextMenu = new ContextMenuStrip();
        var openItem = new ToolStripMenuItem("Open SecApper", null, (s, e) => OpenRequested?.Invoke());
        openItem.Font = new Font(openItem.Font, FontStyle.Bold);
        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(new ToolStripSeparator());

        contextMenu.Items.Add(new ToolStripMenuItem("🔒 Lock All", null, (s, e) => LockAllRequested?.Invoke()));
        contextMenu.Items.Add(new ToolStripMenuItem("🚨 Panic Lock", null, (s, e) => PanicLockRequested?.Invoke()));
        contextMenu.Items.Add(new ToolStripSeparator());

        contextMenu.Items.Add(new ToolStripMenuItem("Exit", null, (s, e) => ExitRequested?.Invoke()));

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => OpenRequested?.Invoke();
    }

    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        try
        {
            _notifyIcon.ShowBalloonTip(3000, title, message, icon);
        }
        catch
        {
            // Balloon tip failure is non-fatal
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
