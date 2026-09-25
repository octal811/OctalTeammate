using System.Threading;
using System.Windows;
using OctalPulse.Installer.Services;
using OctalPulse.Installer.ViewModels;

namespace OctalPulse.Installer;

public partial class App : System.Windows.Application
{
    private const string MutexName = "OctalPulse_Installer_Mutex_A19E5F2D";
    private Mutex? _instanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prevent two installer instances from executing concurrently
        _instanceMutex = new Mutex(initiallyOwned: true, name: MutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            InstallerLogger.Warn("Another instance of OctalPulse.Installer is already running. Exiting.");
            System.Windows.MessageBox.Show(
                "Another instance of OctalPulse Installer is already running.",
                "OctalPulse Installer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var viewModel = new InstallerViewModel();
        var mainWindow = new MainWindow(viewModel);
        mainWindow.Show();

        // Process command line options (e.g. --update, --path, --version, --url, --reinstall)
        await viewModel.InitializeWithCommandLineAsync(e.Args);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_instanceMutex != null)
        {
            try { _instanceMutex.ReleaseMutex(); } catch { }
            _instanceMutex.Dispose();
        }
        base.OnExit(e);
    }
}
