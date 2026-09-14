using System.Windows;
using WinForms = System.Windows.Forms;

namespace OctalPulse.Services;

/// <summary>
/// Manages the system-tray (NotifyIcon) lifecycle for the background-mode feature.
/// </summary>
public sealed class TrayService : IDisposable
{
    private WinForms.NotifyIcon? _notifyIcon;
    private bool _disposed;

    public event Action? ShowMainWindowRequested;
    public event Action? ExitRequested;

    public void Initialize()
    {
        _notifyIcon = new WinForms.NotifyIcon
        {
            Text = "OctalPulse",
            Visible = true
        };

        // Load icon from the embedded resource
        var iconUri = new Uri("pack://application:,,,/OctalPulse;component/Assets/OctalPulse.ico");
        var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
        if (streamInfo != null)
        {
            _notifyIcon.Icon = new Icon(streamInfo.Stream);
        }
        else
        {
            // Fallback to system icon
            _notifyIcon.Icon = SystemIcons.Application;
        }

        // Context menu
        var menu = new WinForms.ContextMenuStrip();

        var showItem = new WinForms.ToolStripMenuItem("Show OctalPulse");
        showItem.Font = new System.Drawing.Font(showItem.Font, System.Drawing.FontStyle.Bold);
        showItem.Click += (_, _) => ShowMainWindowRequested?.Invoke();
        menu.Items.Add(showItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        var exitItem = new WinForms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;

        // Double-click restores main window
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindowRequested?.Invoke();
    }

    public void ShowBalloonTip(string title, string message, int durationMs = 2000)
    {
        _notifyIcon?.ShowBalloonTip(durationMs, title, message, WinForms.ToolTipIcon.None);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
