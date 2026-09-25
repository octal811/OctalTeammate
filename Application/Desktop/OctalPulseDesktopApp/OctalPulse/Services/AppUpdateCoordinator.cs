using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;

namespace OctalPulse.Services;

public class AppUpdateCoordinator
{
    private readonly IUpdateCheckService _updateCheckService;
    private readonly ILogger<AppUpdateCoordinator> _logger;

    public AppUpdateCoordinator(
        IUpdateCheckService updateCheckService,
        ILogger<AppUpdateCoordinator> logger)
    {
        _updateCheckService = updateCheckService;
        _logger = logger;
    }

    /// <summary>
    /// Checks if the previous update attempt was interrupted before completion.
    /// Returns true if interrupted and handled (app should abort normal startup).
    /// </summary>
    public bool CheckAndHandleInterruptedUpdate()
    {
        if (!_updateCheckService.IsInterruptedInstallation())
            return false;

        _logger.LogWarning("Interrupted installation detected on startup.");

        var dialog = new InterruptedUpdateDialog();
        var result = dialog.ShowDialog();

        if (result == true && dialog.UserChoseReinstall)
        {
            var installPath = _updateCheckService.GetCurrentInstallPath();
            LaunchInstaller($"--reinstall --path \"{installPath}\"");
            System.Windows.Application.Current?.Shutdown();
            return true;
        }

        // If user canceled/exited, exit app safely to prevent running corrupt files
        System.Windows.Application.Current?.Shutdown();
        return true;
    }

    /// <summary>
    /// Performs a background check for updates without blocking startup or login.
    /// If an update is available, prompts the user.
    /// </summary>
    public void StartBackgroundUpdateCheck()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // Small delay to allow the main window and UI to finish initial rendering
                await Task.Delay(TimeSpan.FromSeconds(4));

                var result = await _updateCheckService.CheckForUpdatesAsync();
                if (result.IsUpdateAvailable && result.NewVersion != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        PromptUserForUpdate(result);
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Silent background update check encountered an error.");
            }
        });
    }

    public void PromptUserForUpdate(UpdateCheckResult result)
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        var dialog = new UpdateNotificationDialog(result);
        if (owner != null && owner.IsVisible)
        {
            dialog.Owner = owner;
        }

        var dialogResult = dialog.ShowDialog();
        if (dialogResult == true && dialog.UserChoseUpdate)
        {
            ApplyUpdate(result);
        }
    }

    public void ApplyUpdate(UpdateCheckResult result)
    {
        var installPath = _updateCheckService.GetCurrentInstallPath();
        var versionArg = result.NewVersion?.DisplayString ?? string.Empty;
        var urlArg = result.DownloadUrl ?? string.Empty;

        var args = $"--update --path \"{installPath}\"";
        if (!string.IsNullOrEmpty(versionArg))
            args += $" --version \"{versionArg}\"";
        if (!string.IsNullOrEmpty(urlArg))
            args += $" --url \"{urlArg}\"";

        _logger.LogInformation("Launching installer with arguments: {Args}", args);

        bool launched = LaunchInstaller(args);
        if (launched)
        {
            _logger.LogInformation("Installer launched successfully. Shutting down OctalPulse.");
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                System.Windows.Application.Current.Shutdown();
            });
        }
        else
        {
            _logger.LogError("Could not locate or launch OctalPulse.Installer.exe.");
            System.Windows.MessageBox.Show(
                "Could not find OctalPulse.Installer.exe. Please ensure the installer is present in the application directory.",
                "Update Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public bool LaunchInstaller(string arguments)
    {
        var installerPath = ResolveInstallerPath();
        if (string.IsNullOrEmpty(installerPath) || !File.Exists(installerPath))
        {
            _logger.LogError("Installer executable not found at resolved location.");
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = arguments,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch installer process from {Path}", installerPath);
            return false;
        }
    }

    public string? ResolveInstallerPath()
    {
        // 1. Same directory as current running executable
        var baseDir = AppContext.BaseDirectory;
        var local = Path.Combine(baseDir, "OctalPulse.Installer.exe");
        if (File.Exists(local)) return local;

        // 2. Global %LocalAppData%\OctalPulse location
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var global = Path.Combine(appData, "OctalPulse", "OctalPulse.Installer.exe");
        if (File.Exists(global)) return global;

        // 3. Parent directory search (common when debugging or running from bin/Debug)
        var devPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "OctalPulseInstaller", "OctalPulse.Installer", "bin", "Debug", "net10.0-windows", "OctalPulse.Installer.exe"));
        if (File.Exists(devPath)) return devPath;

        var legacyDevPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "OctalPulse.Installer", "bin", "Debug", "net10.0-windows", "OctalPulse.Installer.exe"));
        if (File.Exists(legacyDevPath)) return legacyDevPath;

        return local;
    }
}
