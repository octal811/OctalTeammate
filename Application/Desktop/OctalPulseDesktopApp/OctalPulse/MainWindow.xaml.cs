using System.Windows;
using System.Windows.Controls;
using OctalPulse.ViewModels;

namespace OctalPulse;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

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
        Close();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        var isMaximized = WindowState == WindowState.Maximized;

        MaximizeGlyph.Text = isMaximized ? "\uE923" : "\uE922";

        if (isMaximized)
        {
            // When maximized, WindowChrome leaves an invisible resize border between the
            // client area and the screen edge (painted black by Windows). Pull the content
            // out by that width so it covers the whole screen, and let the window size a
            // touch beyond the work area.
            var bleed = SystemParameters.WindowResizeBorderThickness.Left;
            RootLayout.Margin = new Thickness(-bleed, -bleed, -bleed, -bleed);
            MaxWidth = SystemParameters.WorkArea.Width + bleed * 2;
            MaxHeight = SystemParameters.WorkArea.Height + bleed * 2;
        }
        else
        {
            RootLayout.Margin = new Thickness(0);
            MaxWidth = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;
        }
    }
}