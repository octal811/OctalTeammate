using System.Windows;
using OctalPulse.Installer.ViewModels;

namespace OctalPulse.Installer;

public partial class MainWindow : Window
{
    private readonly InstallerViewModel _viewModel;

    public MainWindow(InstallerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
