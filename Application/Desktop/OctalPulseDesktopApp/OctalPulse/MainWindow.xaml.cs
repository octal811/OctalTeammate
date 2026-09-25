using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using OctalPulse.Services;
using OctalPulse.ViewModels;

namespace OctalPulse;

public partial class MainWindow : Window
{
    private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    private const double MinWidthDips = 1050;
    private const double MinHeightDips = 680;

    private readonly MainViewModel _viewModel;
    private readonly TrayService _trayService;
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly FloatWindowService _floatWindowService;

    public MainWindow(
        MainViewModel viewModel,
        TrayService trayService,
        GlobalHotkeyService hotkeyService,
        FloatWindowService floatWindowService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _trayService = trayService;
        _hotkeyService = hotkeyService;
        _floatWindowService = floatWindowService;

        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
        SourceInitialized += Window_SourceInitialized;
        Closed += MainWindow_Closed;
    }

    private static void LogMW(string msg)
    {
        try
        {
            var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OctalPulse", "startup_debug.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] [MainWindow] {msg}\n");
        }
        catch { }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LogMW("MainWindow_Loaded started.");
        try
        {
            // Initialize tray icon
            LogMW("Initializing TrayService...");
            _trayService.Initialize();
            _trayService.ShowMainWindowRequested += OnShowMainWindowRequested;
            _trayService.ExitRequested += OnExitRequested;
            LogMW("TrayService initialized.");

            // Wire hotkeys
            LogMW("Wiring hotkeys...");
            _hotkeyService.HotkeyFired += OnHotkeyFired;
            LogMW("Hotkeys wired.");

            LogMW("Calling MainViewModel.InitializeAsync()...");
            await _viewModel.InitializeAsync();
            LogMW("MainViewModel.InitializeAsync() completed.");
        }
        catch (Exception ex)
        {
            LogMW($"ERROR in MainWindow_Loaded: {ex}");
        }
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        LogMW("Window_SourceInitialized started.");
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            var hwndSource = HwndSource.FromHwnd(handle);
            hwndSource?.AddHook(WndProc);

            // Attach global hotkeys to this window's message pump
            _hotkeyService.Attach(hwndSource!);
            LogMW("Window_SourceInitialized completed.");
        }
        catch (Exception ex)
        {
            LogMW($"ERROR in Window_SourceInitialized: {ex}");
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _floatWindowService.HideAll();
        _hotkeyService.Dispose();
        _trayService.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    // ── Tray handlers ──────────────────────────────────────────────

    private void OnShowMainWindowRequested()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
    }

    private void OnExitRequested()
    {
        Dispatcher.Invoke(() =>
        {
            // Detach events so no double-dispose
            _trayService.ShowMainWindowRequested -= OnShowMainWindowRequested;
            _trayService.ExitRequested -= OnExitRequested;
            System.Windows.Application.Current.Shutdown();
        });
    }

    // ── Hotkey dispatcher ─────────────────────────────────────────

    private void OnHotkeyFired(int id)
    {
        Dispatcher.Invoke(() =>
        {
            switch (id)
            {
                case GlobalHotkeyService.HK_SHOW_MAIN:
                    OnShowMainWindowRequested();
                    break;
                case GlobalHotkeyService.HK_MAJOR_TASKS:
                    _floatWindowService.Toggle(FloatWindowType.MajorTasks);
                    break;
                case GlobalHotkeyService.HK_MINOR_TASKS:
                    _floatWindowService.Toggle(FloatWindowType.MinorTasks);
                    break;
                case GlobalHotkeyService.HK_STOPWATCH:
                    _floatWindowService.Toggle(FloatWindowType.Stopwatch);
                    break;
                case GlobalHotkeyService.HK_CALENDAR:
                    _floatWindowService.Toggle(FloatWindowType.Calendar);
                    break;
                case GlobalHotkeyService.HK_MEDIA:
                    _floatWindowService.Toggle(FloatWindowType.Media);
                    break;
            }
        });
    }

    // ── Title bar button handlers ────────────────────────────────

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Close = full exit (same as before)
        Close();
    }

    private void HideToTrayButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        _trayService.ShowBalloonTip("OctalPulse", "Running in background. Press Alt+W or double-click the tray icon to restore.");
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    // ── WndProc for MINMAXINFO ───────────────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO))!;
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (GetMonitorInfo(monitor, ref monitorInfo))
            {
                var workArea = monitorInfo.rcWork;
                var monitorArea = monitorInfo.rcMonitor;

                mmi.ptMaxSize.X = workArea.Right - workArea.Left;
                mmi.ptMaxSize.Y = workArea.Bottom - workArea.Top;
                mmi.ptMaxPosition.X = workArea.Left - monitorArea.Left;
                mmi.ptMaxPosition.Y = workArea.Top - monitorArea.Top;
                mmi.ptMaxTrackSize.X = mmi.ptMaxSize.X;
                mmi.ptMaxTrackSize.Y = mmi.ptMaxSize.Y;
            }
        }

        var dpi = GetDpiForWindow(hwnd);
        var scale = dpi <= 0 ? 1.0 : dpi / 96.0;
        mmi.ptMinTrackSize.X = (int)Math.Round(MinWidthDips * scale);
        mmi.ptMinTrackSize.Y = (int)Math.Round(MinHeightDips * scale);

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    // ── P/Invoke structs ──────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr handle);
}