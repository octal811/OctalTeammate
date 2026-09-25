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
    AppNotFound,          // Shows 2 buttons: "Install OctalPulse" & "Locate Existing App Folder"
    SelectInstallFolder,  // Path selection for new install
    CheckingUpdates,      // Spinner checking GitHub
    UpdateAvailable,      // Shows update details, "Download & Update Now"
    UpToDate,             // "You are on the latest version"
    Working,              // Downloading / Extracting / Replacing with progress
    DownloadFailed,       // Interrupted download with Resume / Retry
    InterruptedNotice,    // Interrupted previous install recovery
    Completed,            // Finished, "Launch OctalPulse"
    Error                 // General error screen
}

public partial class InstallerViewModel : ObservableObject
{
    private readonly InstallationManager _manager;
    private readonly GitHubReleaseService _gitHubService;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private InstallerStep _currentStep = InstallerStep.CheckingUpdates;

    [ObservableProperty]
    private string _installDirectory = string.Empty;

    [ObservableProperty]
    private string _installedVersion = "None";

    [ObservableProperty]
    private string _targetVersion = "1.0.0.0";

    [ObservableProperty]
    private string _releaseTitle = string.Empty;

    [ObservableProperty]
    private string? _releaseNotes;

    [ObservableProperty]
    private string _packageSizeDisplay = string.Empty;

    [ObservableProperty]
    private string _downloadUrl = string.Empty;

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
    private bool _canPause;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private bool _hasValidationError;

    public InstallerMode Mode { get; private set; } = InstallerMode.FirstInstall;

    public InstallerViewModel(InstallationManager? manager = null, GitHubReleaseService? gitHubService = null)
    {
        _manager = manager ?? new InstallationManager();
        _gitHubService = gitHubService ?? new GitHubReleaseService();
        InstallDirectory = _manager.GetStoredInstallDirectory();
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

        if (isReinstall)
        {
            Mode = InstallerMode.Reinstall;
            await StartUpdateOrInstallProcessAsync();
            return;
        }

        if (isUpdate)
        {
            Mode = InstallerMode.Update;
            await StartUpdateOrInstallProcessAsync();
            return;
        }

        // Default startup: inspect the desktop app path and GitHub releases
        await EvaluateAppPathAndCheckUpdatesAsync();
    }

    /// <summary>
    /// Checks if OctalPulse is installed and healthy at InstallDirectory.
    /// If lost/not found: switches to AppNotFound step (presenting the 2 buttons).
    /// If found: switches to CheckingUpdates to compare against GitHub release tag.
    /// </summary>
    public async Task EvaluateAppPathAndCheckUpdatesAsync()
    {
        HasValidationError = false;
        ValidationMessage = string.Empty;

        // Check if desktop app exists at current path
        bool isValid = _manager.ValidateAppFolder(InstallDirectory, out var detectedVer, out var reason);

        if (!isValid)
        {
            InstallerLogger.Warn($"Desktop app not found at '{InstallDirectory}': {reason}");
            InstalledVersion = "Not Installed";
            CurrentStep = InstallerStep.AppNotFound;
            return;
        }

        // App is validly installed!
        InstalledVersion = detectedVer;
        _manager.SaveInstallDirectory(InstallDirectory);
        InstallerLogger.Info($"Desktop app validated at '{InstallDirectory}'. Current version: v{InstalledVersion}");

        // Now check GitHub releases
        await CheckForUpdatesAgainstGitHubAsync();
    }

    /// <summary>
    /// Queries GitHub releases API for the latest release tag (e.g. v1.7.5.12).
    /// </summary>
    [RelayCommand]
    public async Task CheckForUpdatesAgainstGitHubAsync()
    {
        CurrentStep = InstallerStep.CheckingUpdates;
        StatusMessage = "Checking for updates...";
        SubStatusMessage = "Connecting to GitHub Releases...";
        IsIndeterminate = true;

        try
        {
            var release = await _gitHubService.GetLatestReleaseAsync();
            if (release == null)
            {
                // If offline or release check failed, but app is present
                StatusMessage = "Could not check GitHub releases";
                SubStatusMessage = "Please verify your internet connection. You can still launch the installed app.";
                CurrentStep = InstallerStep.UpToDate;
                return;
            }

            TargetVersion = release.Version.DisplayString;
            ReleaseTitle = release.Title;
            ReleaseNotes = release.ReleaseNotes;
            DownloadUrl = release.DownloadUrl;
            PackageSizeDisplay = (release.PackageSizeBytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";

            var localVer = AppVersionInfo.FromString(InstalledVersion);
            var remoteVer = release.Version;

            InstallerLogger.Info($"Comparing versions: Local=v{localVer.DisplayString}, Remote=v{remoteVer.DisplayString}");

            if (remoteVer.CompareTo(localVer) > 0)
            {
                // New update available!
                Mode = InstallerMode.Update;
                CurrentStep = InstallerStep.UpdateAvailable;
            }
            else
            {
                // Already on latest version!
                CurrentStep = InstallerStep.UpToDate;
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.Error("Error checking GitHub releases", ex);
            SubStatusMessage = "Could not connect to update servers.";
            CurrentStep = InstallerStep.UpToDate;
        }
    }

    #region Lost Path / App Not Found Navigation Commands

    /// <summary>
    /// Button 1: User chose to install a new instance of OctalPulse.
    /// </summary>
    [RelayCommand]
    private void GoToInstallNew()
    {
        HasValidationError = false;
        ValidationMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(InstallDirectory) || !Directory.Exists(InstallDirectory))
        {
            InstallDirectory = InstallationManager.GetDefaultInstallDirectory();
        }
        CurrentStep = InstallerStep.SelectInstallFolder;
    }

    /// <summary>
    /// Button 2: User moved the app and wants the installer to find/re-link it.
    /// </summary>
    [RelayCommand]
    private async Task LocateAppFolderAsync()
    {
        HasValidationError = false;
        ValidationMessage = string.Empty;

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Folder Containing OctalPulse.exe",
            InitialDirectory = Directory.Exists(InstallDirectory) ? InstallDirectory : Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        if (dialog.ShowDialog() == true)
        {
            var selectedFolder = dialog.FolderName;
            bool ok = _manager.ValidateAppFolder(selectedFolder, out var detectedVer, out var reason);

            if (ok)
            {
                InstallDirectory = selectedFolder;
                InstalledVersion = detectedVer;
                _manager.SaveInstallDirectory(selectedFolder);
                InstallerLogger.Info($"User successfully relocated app folder: {selectedFolder} (v{detectedVer})");

                // Immediately check for updates for this newly located path!
                await CheckForUpdatesAgainstGitHubAsync();
            }
            else
            {
                HasValidationError = true;
                ValidationMessage = $"OctalPulse.exe was not found in '{Path.GetFileName(selectedFolder)}'. Please select the folder containing OctalPulse.exe.";
                InstallerLogger.Warn($"Invalid app folder chosen: {reason}");
            }
        }
    }

    [RelayCommand]
    private void BrowseNewInstallFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose Installation Directory",
            InitialDirectory = Directory.Exists(InstallDirectory) ? InstallDirectory : InstallationManager.GetDefaultInstallDirectory()
        };

        if (dialog.ShowDialog() == true)
        {
            InstallDirectory = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BackToAppNotFound()
    {
        CurrentStep = InstallerStep.AppNotFound;
    }

    #endregion

    #region Install & Update Execution

    [RelayCommand]
    private async Task StartInstallAsync()
    {
        Mode = InstallerMode.FirstInstall;
        await StartUpdateOrInstallProcessAsync();
    }

    [RelayCommand]
    private async Task StartUpdateAsync()
    {
        Mode = InstallerMode.Update;
        await StartUpdateOrInstallProcessAsync();
    }

    [RelayCommand]
    private async Task StartReinstallAsync()
    {
        Mode = InstallerMode.Reinstall;
        await StartUpdateOrInstallProcessAsync();
    }

    [RelayCommand]
    private void PauseDownload()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            InstallerLogger.Info("User paused the download.");
            IsPaused = true;
            _cts.Cancel();
            StatusMessage = "Download Paused";
            SubStatusMessage = "Download paused. Click Resume to continue.";
            CanPause = false;
        }
    }

    [RelayCommand]
    private async Task ResumeDownloadAsync()
    {
        IsPaused = false;
        await StartUpdateOrInstallProcessAsync();
    }

    [RelayCommand]
    private void CancelOperation()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            InstallerLogger.Info("User cancelled the operation.");
            _cts.Cancel();
        }

        // Clean up partial download on explicit cancel
        var tempDir = InstallationManager.GetTempUpdateDirectory();
        var tempPackage = Path.Combine(tempDir, "OctalPulse-Package.download");
        _manager.CleanupPartialDownload(tempPackage);

        StatusMessage = "Operation Cancelled";
        SubStatusMessage = "Existing application was left untouched.";
        CurrentStep = InstallerStep.DownloadFailed;
        CanCancel = false;
        CanPause = false;
        IsPaused = false;
    }

    [RelayCommand]
    private async Task RetryAsync()
    {
        IsPaused = false;
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
            ErrorMessage = $"Failed to start OctalPulse from {InstallDirectory}. Please make sure OctalPulse.exe is present.";
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
        if (!IsPaused)
        {
            ProgressPercent = 0;
        }
        IsIndeterminate = true;
        CanCancel = true;
        CanPause = true;
        IsPaused = false;
        ErrorMessage = string.Empty;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        var tempDir = InstallationManager.GetTempUpdateDirectory();
        var tempDownloadFile = Path.Combine(tempDir, "OctalPulse-Package.download");
        var tempExtracted = Path.Combine(tempDir, "Extracted");

        try
        {
            // ── Step 1: Wait for running app to exit ──
            StatusMessage = "Waiting for OctalPulse to exit...";
            SubStatusMessage = "Checking if OctalPulse is currently running...";
            var progressText = new Progress<string>(msg => SubStatusMessage = msg);

            bool exited = await _manager.WaitForApplicationExitAsync(TimeSpan.FromSeconds(30), progressText, ct);
            if (!exited)
            {
                ErrorMessage = "OctalPulse is still running. Please close the app and try again.";
                CurrentStep = InstallerStep.Error;
                return;
            }

            // ── Step 2: Resolve download URL if needed ──
            if (string.IsNullOrEmpty(DownloadUrl))
            {
                StatusMessage = "Resolving latest package from GitHub...";
                SubStatusMessage = "Connecting to GitHub Releases...";
                var release = await _gitHubService.GetLatestReleaseAsync(ct: ct);
                if (release != null)
                {
                    TargetVersion = release.Version.DisplayString;
                    DownloadUrl = release.DownloadUrl;
                }
            }

            if (string.IsNullOrEmpty(DownloadUrl))
            {
                ErrorMessage = "Could not resolve download URL for the package from GitHub.";
                CurrentStep = InstallerStep.Error;
                return;
            }

            // ── Step 3: Resumable Safe Download (Existing App 100% Untouched) ──
            StatusMessage = $"Downloading OctalPulse v{TargetVersion}...";
            SubStatusMessage = "Connecting to download server...";
            IsIndeterminate = false;

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

            await _manager.DownloadPackageAsync(DownloadUrl, tempDownloadFile, downloadProgress, ct);
            CanPause = false;

            // ── Step 4: Extract Archive (Existing App Still Untouched) ──
            StatusMessage = "Extracting package...";
            SubStatusMessage = "Verifying package integrity before modifying files...";
            IsIndeterminate = true;
            var extractProgress = new Progress<string>(msg => SubStatusMessage = msg);

            await _manager.ExtractPackageAsync(tempDownloadFile, tempExtracted, extractProgress, ct);

            // ── Step 5: Clean Folder Replacement (Atomic Backup & Replace) ──
            StatusMessage = "Installing new version...";
            SubStatusMessage = "Replacing application files cleanly...";
            IsIndeterminate = false;

            var replaceProgress = new Progress<(string status, double percent)>(p =>
            {
                SubStatusMessage = p.status;
                ProgressPercent = p.percent;
            });

            await _manager.ReplaceApplicationFolderAsync(tempExtracted, InstallDirectory, TargetVersion, replaceProgress, ct);

            // ── Step 6: Cleanup Temp Files ──
            StatusMessage = "Finalizing...";
            SubStatusMessage = "Cleaning up temporary files...";
            IsIndeterminate = true;
            _manager.CleanupTemp(tempDir);

            // ── Step 7: Completed & Ready ──
            InstalledVersion = TargetVersion;
            StatusMessage = "You are up to date!";
            SubStatusMessage = $"OctalPulse v{TargetVersion} is ready in {InstallDirectory}.";
            ProgressPercent = 100;
            IsIndeterminate = false;
            CurrentStep = InstallerStep.Completed;

            InstallerLogger.Info($"Setup successful. Version v{TargetVersion} ready in {InstallDirectory}.");
        }
        catch (OperationCanceledException)
        {
            if (IsPaused)
            {
                StatusMessage = "Download Paused";
                SubStatusMessage = "Your partial download is saved. Click Resume to continue.";
                CurrentStep = InstallerStep.DownloadFailed;
            }
            else
            {
                StatusMessage = "Download Cancelled";
                SubStatusMessage = "Your existing installation remains completely safe and untouched.";
                CurrentStep = InstallerStep.DownloadFailed;
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.Error("Error during installation/update process", ex);

            if (CurrentStep == InstallerStep.Working && ProgressPercent < 80)
            {
                // Network dropped or failed during download: old version is completely safe!
                StatusMessage = "Download Interrupted";
                SubStatusMessage = "Network connection lost or interrupted. Your existing installation is safe.";
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
            CanPause = false;
        }
    }

    #endregion
}
