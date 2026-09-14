using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace OctalPulse;

/// <summary>
/// Custom entry point so we can enforce single-instance BEFORE WPF initialises.
/// The WPF-generated Main is suppressed via &lt;ApplicationEntryPoint&gt; in the csproj.
/// </summary>
public static class Program
{
    private const string MutexName  = "OctalPulse_SingleInstance_3F8A2B1C";
    private const string EventName  = "OctalPulse_ActivateEvent_3F8A2B1C";
    private const int    SW_RESTORE = 9;

    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int pid);

    [STAThread]
    public static void Main(string[] args)
    {
        // Open (or create) the named event used for cross-process signalling.
        using var activateEvent = new EventWaitHandle(
            initialState: false,
            mode:         EventResetMode.AutoReset,
            name:         EventName);

        // Try to acquire the mutex – first instance wins, second exits.
        using var mutex = new Mutex(initiallyOwned: true, name: MutexName, out bool isFirstInstance);

        if (!isFirstInstance)
        {
            // Signal the already-running instance to bring itself to the front.
            activateEvent.Set();
            return; // Exit this second process immediately.
        }

        // ── First instance: register a background watcher for activation signals ──
        RegisteredWaitHandle? waitHandle = null;
        waitHandle = ThreadPool.RegisterWaitForSingleObject(
            activateEvent,
            (_, _) => ActivateMainWindow(),
            null,
            Timeout.Infinite,
            false);

        try
        {
            // Now start WPF normally.
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        finally
        {
            waitHandle.Unregister(null);
            try { mutex.ReleaseMutex(); } catch { /* already released */ }
        }
    }

    /// <summary>
    /// Invoked on a thread-pool thread when a second instance signals us.
    /// Forces the main window to the foreground, even if minimised or tray-hidden.
    /// </summary>
    private static void ActivateMainWindow()
    {
        // Try the Win32 window handle first (fastest path).
        var current = Process.GetCurrentProcess();
        var hwnd    = current.MainWindowHandle;

        if (hwnd != IntPtr.Zero)
        {
            AllowSetForegroundWindow(current.Id);
            ShowWindowAsync(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
            return;
        }

        // Window is hidden (e.g. minimised to tray) — dispatch to the UI thread.
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var win = System.Windows.Application.Current.MainWindow;
            if (win == null) return;
            win.Show();
            win.WindowState = System.Windows.WindowState.Normal;
            win.Activate();
        });
    }
}
