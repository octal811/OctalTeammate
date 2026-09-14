using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using OctalPulse.Services;
using OctalPulse.ViewModels;

namespace OctalPulse.FloatWindows;

public partial class MinorTasksFloatWindow : Window
{
    private const string PositionKey = "MinorTasks";
    private static readonly int ScreenHeight = (int)SystemParameters.PrimaryScreenHeight;

    private readonly MinorTasksFloatViewModel _viewModel;
    private readonly FloatWindowPositionStore _positionStore;
    private bool _isLocked;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    public MinorTasksFloatWindow(
        MinorTasksFloatViewModel viewModel,
        FloatWindowPositionStore positionStore)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _positionStore = positionStore;
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Activated += OnActivated;
        Closing += OnWindowClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var pos = _positionStore.Get(PositionKey);
        if (pos != null)
        {
            Left = pos.Left;
            Top = pos.Top;
            _isLocked = pos.IsLocked;
            UpdateLockGlyph();
        }
        else
        {
            // Default: left of MajorTasksWindow
            Left = Math.Max(20, SystemParameters.WorkArea.Right - Width - 410);
            Top = Math.Max(20, (SystemParameters.WorkArea.Height - Height) / 2);
        }

        SendToDesktopLevel();
        await _viewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void OnActivated(object? sender, EventArgs e) => SendToDesktopLevel();

    private void SendToDesktopLevel()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isLocked) DragMove();
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        _isLocked = !_isLocked;
        UpdateLockGlyph();
        _positionStore.Set(PositionKey, Left, Top, _isLocked);
    }

    private void UpdateLockGlyph()
    {
        LockGlyph.Text = _isLocked ? "\uE785" : "\uE8A0";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    public new void Show()
    {
        base.Show();
        SendToDesktopLevel();
        if (_viewModel.ActiveTasks.Count == 0 && !_viewModel.IsBusy)
        {
            _ = _viewModel.RefreshCommand.ExecuteAsync(null);
        }
    }
}
