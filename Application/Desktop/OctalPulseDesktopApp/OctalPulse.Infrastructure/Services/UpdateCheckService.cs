using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;

namespace OctalPulse.Infrastructure.Services;

public class UpdateCheckService : IUpdateCheckService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UpdateCheckService> _logger;

    private const string InstallationFileName = "installation.json";
    private const string MainExecutableName = "OctalPulse.exe";

    public UpdateCheckService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<UpdateCheckService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = AppVersionInfo.Current;
        _logger.LogInformation("Update check started. Current version: {CurrentVersion}", currentVersion.DisplayString);

        var manifestUrl = _configuration["Update:ManifestUrl"]
            ?? "https://raw.githubusercontent.com/octal811/OctalTeammate/main/releases/update.json";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
            request.Headers.UserAgent.ParseAdd("OctalPulse-AutoUpdater/1.0");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = $"Failed to retrieve update manifest from {manifestUrl} (Status: {response.StatusCode})";
                _logger.LogWarning(errorMsg);
                return new UpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    CurrentVersion = currentVersion,
                    ErrorMessage = errorMsg
                };
            }

            var manifest = await response.Content.ReadFromJsonAsync<UpdateManifest>(cancellationToken: cancellationToken);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                var errorMsg = "Update manifest was empty or invalid.";
                _logger.LogWarning(errorMsg);
                return new UpdateCheckResult
                {
                    IsUpdateAvailable = false,
                    CurrentVersion = currentVersion,
                    ErrorMessage = errorMsg
                };
            }

            var remoteVersion = AppVersionInfo.FromString(manifest.Version);
            _logger.LogInformation("Remote version discovered: {RemoteVersion}", remoteVersion.DisplayString);

            // Compare version using System.Version (proper semantic numeric comparison)
            bool isUpdateAvailable = remoteVersion.CompareTo(currentVersion) > 0;

            if (isUpdateAvailable)
            {
                _logger.LogInformation("Update available! {Current} -> {Remote}", currentVersion.DisplayString, remoteVersion.DisplayString);
            }
            else
            {
                _logger.LogInformation("Application is up to date ({Version}).", currentVersion.DisplayString);
            }

            return new UpdateCheckResult
            {
                IsUpdateAvailable = isUpdateAvailable,
                CurrentVersion = currentVersion,
                NewVersion = remoteVersion,
                DownloadUrl = manifest.DownloadUrl,
                ReleaseNotes = manifest.ReleaseNotes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while checking for updates.");
            return new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                CurrentVersion = currentVersion,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<InstallationMetadata?> GetInstallationMetadataAsync()
    {
        var path = ResolveInstallationMetadataFilePath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<InstallationMetadata>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read installation metadata from {Path}", path);
            return null;
        }
    }

    public string GetCurrentInstallPath()
    {
        var meta = GetInstallationMetadataAsync().GetAwaiter().GetResult();
        if (meta != null && !string.IsNullOrWhiteSpace(meta.InstallPath) && Directory.Exists(meta.InstallPath))
        {
            return meta.InstallPath;
        }

        return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public bool IsInterruptedInstallation()
    {
        var meta = GetInstallationMetadataAsync().GetAwaiter().GetResult();
        if (meta == null || meta.Installed)
            return false;

        // A genuine interrupted merge stages the app files into the real install folder
        // before flipping "Installed" to true. If the flagged folder no longer exists, or
        // does not contain the app executable, the marker is stale (e.g. leftover from a
        // dev install test) and must not block startup.
        var installPath = meta.InstallPath;
        if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath) ||
            !File.Exists(Path.Combine(installPath, MainExecutableName)))
        {
            _logger.LogWarning("Interrupted-installation marker is stale (no app at {Path}). Ignoring.", installPath);
            return false;
        }

        _logger.LogWarning("Interrupted installation detected with app present at {Path}.", installPath);
        return true;
    }

    private string? ResolveInstallationMetadataFilePath()
    {
        // 1. Check AppContext.BaseDirectory
        var localPath = Path.Combine(AppContext.BaseDirectory, InstallationFileName);
        if (File.Exists(localPath)) return localPath;

        // 2. Check %LocalAppData%\OctalPulse
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var globalPath = Path.Combine(appData, "OctalPulse", InstallationFileName);
        if (File.Exists(globalPath)) return globalPath;

        return localPath;
    }
}
