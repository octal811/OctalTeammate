using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Installer.Models;
using OctalPulse.Installer.Services;

namespace OctalPulse.Installer.ViewModels;

public enum InstallerMode
{
    FirstInstall,
    Update,
    Reinstall
}

public enum InstallerStep
{
    Welcome,
    InterruptedNotice,
    Working,
    DownloadFailed,
    Completed,
    Error
}

public partial class InstallerViewModel : ObservableObject
{
    private readonly InstallationManager _manager;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private InstallerStep _currentStep = InstallerStep.Welcome;

    [ObservableProperty]
    private string _installDirectory = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _subStatusMessage = string.Empty;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private bool _isIndeterminate;

    [ObservableProperty]
    private bool _canCancel;

    [ObservableProperty]
    private string _targetVersion = "1.0.0.0";

    [ObservableProperty]
    private string _downloadUrl = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public InstallerMode Mode { get; private set; } = InstallerMode.FirstInstall;

    public InstallerViewModel(InstallationManager? manager = null)
    {
        _manager = manager ?? new InstallationManager();
        InstallDirectory = InstallationManager.GetDefaultInstallDirectory();
    }

    public async Task InitializeWithCommandLineAsync(string[] args)
    {
        InstallerLogger.Info($"Installer starting. Arguments: {string.Join(" ", args)}");

        string? customPath = null;
        string? targetVer = null;
        string? customUrl = null;
        bool isUpdate = false;
        bool isReinstall = false;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--update", StringComparison.OrdinalIgnoreCase))
                isUpdate = true;
            else if (arg.Equals("--reinstall", StringComparison.OrdinalIgnoreCase))
                isReinstall = true;
            else if (arg.Equals("--path", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                customPath = args[++i].Trim('"', '\'');
            else if (arg.Equals("--version", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                targetVer = args[++i].Trim('"', '\'');
            else if (arg.Equals("--url", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                customUrl = args[++i].Trim('"', '\'');
        }

        if (!string.IsNullOrEmpty(customPath))
        {
            InstallDirectory = customPath;
        }

        if (!string.IsNullOrEmpty(targetVer))
        {
            TargetVersion = targetVer;
        }

        if (!string.IsNullOrEmpty(customUrl))
        {
            DownloadUrl = customUrl;
        }

        // Check if existing metadata indicates an interrupted update
        var existingMeta = _manager.ReadInstallationMetadata(InstallDirectory);
        if (existingMeta != null)
        {
            if (!string.IsNullOrEmpty(existingMeta.InstallPath))
                InstallDirectory = existingMeta.InstallPath;

            if (!existingMeta.Installed)
            {
                if (IsValidStagedInstall(existingMeta.InstallPath))
                {
                    InstallerLogger.Warn("Detected previous interrupted update in installation directory.");
                    CurrentStep = InstallerStep.InterruptedNotice;
                    Mode = InstallerMode.Reinstall;
                    return;
                }

                InstallerLogger.Warn($"Interrupted-installation marker is stale (no app at {existingMeta.InstallPath}). Ignoring.");
            }
        }

        if (isReinstall)
        {
            Mode = InstallerMode.Reinstall;
            await StartUpdateOrInstallProcessAsync();
        }
        else if (isUpdate)
        {
            Mode = InstallerMode.Update;
            await StartUpdateOrInstallProcessAsync();
        }
        else
        {
            // Interactive mode without update arguments
            // If already installed and healthy, allow updating or reinstalling
            if (existingMeta != null && existingMeta.Installed && File.Exists(Path.Combine(InstallDirectory, InstallationManager.MainExecutableName)))
            {
                TargetVersion = existingMeta.Version;
                CurrentStep = InstallerStep.Welcome;
            }
            else
            {
                Mode = InstallerMode.FirstInstall;
                CurrentStep = InstallerStep.Welcome;
            }
        }
    }

    private bool IsValidStagedInstall(string? installPath)
    {
        return !string.IsNullOrWhiteSpace(installPath)
            && Directory.Exists(installPath)
            && File.Exists(Path.Combine(installPath, InstallationManager.MainExecutableName));
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select OctalPulse Installation Folder",
            InitialDirectory = Directory.Exists(InstallDirectory) ? InstallDirectory : InstallationManager.GetDefaultInstallDirectory()
        };

        if (dialog.ShowDialog() == true)
        {
            InstallDirectory = dialog.FolderName;
        }
    }

    [RelayCommand]
    private async Task StartInstallAsync()
    {
        Mode = InstallerMode.FirstInstall;
        await StartUpdateOrInstallProcessAsync();
    }

    [RelayCommand]
    private async Task StartReinstallAsync()
    {
        Mode = InstallerMode.Reinstall;
        await StartUpdateOrInstallProcessAsync();
    }

    [RelayCommand]
    private void CancelOperation()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            InstallerLogger.Info("User requested cancellation of the download.");
            _cts.Cancel();
            StatusMessage = "Operation cancelled.";
            SubStatusMessage = "Your existing installation was left untouched.";
            CanCancel = false;
        }
    }

    [RelayCommand]
    private async Task RetryAsync()
    {
        await StartUpdateOrInstallProcessAsync();
    }

    [RelayCommand]
    private void LaunchApplication()
    {
        bool ok = _manager.LaunchApplication(InstallDirectory);
        if (ok)
        {
            System.Windows.Application.Current.Shutdown();
        }
        else
        {
            ErrorMessage = $"Failed to start OctalPulse from {InstallDirectory}.";
            CurrentStep = InstallerStep.Error;
        }
    }

    [RelayCommand]
    private void ExitInstaller()
    {
        System.Windows.Application.Current.Shutdown();
    }

    public async Task StartUpdateOrInstallProcessAsync()
    {
        CurrentStep = InstallerStep.Working;
        ProgressPercent = 0;
        IsIndeterminate = true;
        CanCancel = false;
        ErrorMessage = string.Empty;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        var tempDir = InstallationManager.GetTempUpdateDirectory();
        var tempZip = Path.Combine(tempDir, "OctalPulse-Package.zip");
        var tempExtracted = Path.Combine(tempDir, "Extracted");

        try
        {
            // ── Step 1: Wait for running app to exit ──
            StatusMessage = "Waiting for OctalPulse to exit...";
            SubStatusMessage = "Please close OctalPulse if it is still running.";
            var progressText = new Progress<string>(msg => SubStatusMessage = msg);

            bool exited = await _manager.WaitForApplicationExitAsync(TimeSpan.FromSeconds(30), progressText, ct);
            if (!exited)
            {
                ErrorMessage = "OctalPulse is still running. Please close the application and try again.";
                CurrentStep = InstallerStep.Error;
                return;
            }

            // ── Step 2: Resolve download URL if not provided ──
            if (string.IsNullOrEmpty(DownloadUrl))
            {
                StatusMessage = "Checking GitHub release manifest...";
                SubStatusMessage = "Fetching latest version information...";
                var manifest = await _manager.FetchUpdateManifestAsync(InstallationManager.DefaultManifestUrl, ct);
                if (manifest != null)
                {
                    TargetVersion = manifest.Version;
                    DownloadUrl = manifest.DownloadUrl;
                }
            }

            if (string.IsNullOrEmpty(DownloadUrl))
            {
                ErrorMessage = "Could not resolve download URL for the update package.";
                CurrentStep = InstallerStep.Error;
                return;
            }

            // ── Step 3: Safe Download to Temp Directory (Existing App Untouched) ──
            StatusMessage = $"Downloading OctalPulse v{TargetVersion}...";
            SubStatusMessage = "Connecting to download server...";
            IsIndeterminate = false;
            CanCancel = true;

            var downloadProgress = new Progress<(double percent, long downloaded, long total)>(info =>
            {
                if (info.percent >= 0)
                {
                    ProgressPercent = info.percent;
                    var mbDownloaded = (info.downloaded / (1024.0 * 1024.0)).ToString("0.0");
                    var mbTotal = (info.total / (1024.0 * 1024.0)).ToString("0.0");
                    SubStatusMessage = $"{mbDownloaded} MB / {mbTotal} MB ({info.percent:0}%)";
                }
                else
                {
                    IsIndeterminate = true;
                    var mb = (info.downloaded / (1024.0 * 1024.0)).ToString("0.0");
                    SubStatusMessage = $"{mb} MB downloaded...";
                }
            });

            await _manager.DownloadPackageAsync(DownloadUrl, tempZip, downloadProgress, ct);
            CanCancel = false;

            // ── Step 4: Safe Extract to Temp (Existing App Still Untouched) ──
            StatusMessage = "Extracting package...";
            SubStatusMessage = "Verifying package integrity before modifying files...";
            IsIndeterminate = true;
            var extractProgress = new Progress<string>(msg => SubStatusMessage = msg);

            await _manager.ExtractPackageAsync(tempZip, tempExtracted, extractProgress, ct);

            // ── Step 5: Merge / Overwrite Files into Target Directory ──
            StatusMessage = "Updating application files...";
            SubStatusMessage = "Applying new version...";
            IsIndeterminate = false;

            var mergeProgress = new Progress<(string status, double percent)>(p =>
            {
                SubStatusMessage = p.status;
                ProgressPercent = p.percent;
            });

            await _manager.MergeAndCleanAsync(tempExtracted, InstallDirectory, TargetVersion, mergeProgress, ct);

            // ── Step 6: Cleanup Temp Files ──
            StatusMessage = "Finalizing update...";
            SubStatusMessage = "Cleaning up temporary files...";
            IsIndeterminate = true;
            _manager.CleanupTemp(tempDir);

            // ── Step 7: Completed & Launch ──
            StatusMessage = "Installation Complete!";
            SubStatusMessage = $"OctalPulse has been updated to v{TargetVersion}.";
            ProgressPercent = 100;
            IsIndeterminate = false;
            CurrentStep = InstallerStep.Completed;

            InstallerLogger.Info($"Process finished successfully. Version v{TargetVersion} ready in {InstallDirectory}.");

            // Automatically launch app after 1.5 seconds if in update mode
            if (Mode == InstallerMode.Update)
            {
                await Task.Delay(1500);
                LaunchApplication();
            }
        }
        catch (OperationCanceledException)
        {
            InstallerLogger.Info("Installation was cancelled by user.");
            StatusMessage = "Download Cancelled";
            SubStatusMessage = "Your existing installation is still safe and intact.";
            CurrentStep = InstallerStep.DownloadFailed;
        }
        catch (Exception ex)
        {
            InstallerLogger.Error("Error during installation/update process", ex);
            _manager.CleanupTemp(tempDir);

            if (CurrentStep == InstallerStep.Working && ProgressPercent < 80)
            {
                // Failed during download or extraction: old installation is still completely safe!
                StatusMessage = "Download failed";
                SubStatusMessage = "Your current version is still safe and untouched.";
                ErrorMessage = ex.Message;
                CurrentStep = InstallerStep.DownloadFailed;
            }
            else
            {
                ErrorMessage = ex.Message;
                CurrentStep = InstallerStep.Error;
            }
        }
        finally
        {
            CanCancel = false;
        }
    }
}
