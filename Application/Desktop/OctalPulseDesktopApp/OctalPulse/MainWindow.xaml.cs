using System.Windows;
using System.Windows.Interop;
using OctalPulse.Services;
using OctalPulse.ViewModels;

namespace OctalPulse;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ThemeService _themeService;
    private IntPtr _hwnd;

    public MainWindow(MainViewModel viewModel, ThemeService themeService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _themeService = themeService;
        DataContext = _viewModel;

        // SourceInitialized fires as soon as the Win32 HWND is created (before Show/Loaded).
        // This is the earliest safe point to call DWM APIs.
        SourceInitialized += OnSourceInitialized;

        // Re-apply whenever the user changes theme at runtime (e.g. from Settings)
        _themeService.ThemeChanged += OnThemeChanged;

        Loaded += MainWindow_Loaded;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        // Apply the title bar style that matches the current theme
        ThemeService.ApplyTitleBarForTheme(_hwnd, _themeService.CurrentTheme);
    }

    private void OnThemeChanged(string theme)
    {
        // Called on every SetTheme() — re-paint the title bar immediately
        ThemeService.ApplyTitleBarForTheme(_hwnd, theme);
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }
}