using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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

        try
        {
            UpdateManifest? manifest = null;

            // 1. If explicit ManifestUrl is configured, try it first
            var customManifestUrl = _configuration["Update:ManifestUrl"];
            if (!string.IsNullOrWhiteSpace(customManifestUrl))
            {
                manifest = await TryFetchManifestUrlAsync(customManifestUrl, cancellationToken);
            }

            // 2. Query GitHub Releases API directly (same repository source as installer)
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                var owner = _configuration["Update:GitHubOwner"] ?? "octal811";
                var repo = _configuration["Update:GitHubRepo"] ?? "OctalTeammate";
                manifest = await FetchFromGitHubReleasesAsync(owner, repo, cancellationToken);
            }

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                var errorMsg = "Could not retrieve latest release information from GitHub or update server.";
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

    private async Task<UpdateManifest?> TryFetchManifestUrlAsync(string manifestUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
            request.Headers.UserAgent.ParseAdd("OctalPulse-AutoUpdater/1.0");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<UpdateManifest>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch manifest from {Url}", manifestUrl);
            return null;
        }
    }

    private async Task<UpdateManifest?> FetchFromGitHubReleasesAsync(string owner, string repo, CancellationToken ct)
    {
        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/releases";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            request.Headers.UserAgent.ParseAdd("OctalPulse-DesktopApp/1.0");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub Releases API returned status {Status}", response.StatusCode);
                return null;
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            // Find the latest non-draft release
            JsonElement targetRelease = default;
            bool found = false;
            foreach (var rel in doc.RootElement.EnumerateArray())
            {
                if (rel.TryGetProperty("draft", out var draftProp) && draftProp.GetBoolean())
                    continue;

                targetRelease = rel;
                found = true;
                break;
            }

            if (!found)
                targetRelease = doc.RootElement[0];

            var tagName = targetRelease.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(tagName))
                return null;

            var cleanVer = tagName.Trim().TrimStart('v', 'V');
            var notes = targetRelease.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : null;

            // Find distribution asset (.rar, .zip, .7z)
            string downloadUrl = string.Empty;
            if (targetRelease.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : string.Empty;
                    var urlProp = asset.TryGetProperty("browser_download_url", out var bUrl) ? bUrl.GetString() : string.Empty;
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(urlProp))
                    {
                        if (name.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = urlProp;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl) && assetsProp.GetArrayLength() > 0)
                {
                    downloadUrl = assetsProp[0].TryGetProperty("browser_download_url", out var bUrl) ? bUrl.GetString() ?? "" : "";
                }
            }

            DateTime? releasedAt = null;
            if (targetRelease.TryGetProperty("published_at", out var pubProp) && pubProp.TryGetDateTime(out var dt))
            {
                releasedAt = dt;
            }

            return new UpdateManifest
            {
                Version = cleanVer,
                DownloadUrl = downloadUrl,
                ReleaseNotes = notes,
                ReleasedAt = releasedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query GitHub releases.");
            return null;
        }
    }

    public Task<InstallationMetadata?> GetInstallationMetadataAsync()
    {
        return Task.FromResult(ReadInstallationMetadata());
    }

    private InstallationMetadata? ReadInstallationMetadata()
    {
        var path = ResolveInstallationMetadataFilePath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
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
        var meta = ReadInstallationMetadata();
        if (meta != null && !string.IsNullOrWhiteSpace(meta.InstallPath) && Directory.Exists(meta.InstallPath))
        {
            return meta.InstallPath;
        }

        return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public bool IsInterruptedInstallation()
    {
        var meta = ReadInstallationMetadata();
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
