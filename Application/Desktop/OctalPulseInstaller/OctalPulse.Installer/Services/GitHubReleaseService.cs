using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OctalPulse.Installer.Models;

namespace OctalPulse.Installer.Services;

public sealed class GitHubReleaseInfo
{
    public string TagName { get; set; } = string.Empty;
    public AppVersionInfo Version { get; set; } = new(new Version(1, 0, 0, 0));
    public string Title { get; set; } = string.Empty;
    public string? ReleaseNotes { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public string PackageFileName { get; set; } = string.Empty;
    public long PackageSizeBytes { get; set; }
    public string HtmlUrl { get; set; } = string.Empty;
}

public class GitHubReleaseService
{
    private readonly HttpClient _httpClient;
    public const string DefaultOwner = "octal811";
    public const string DefaultRepo = "OctalTeammate";

    public GitHubReleaseService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctalPulse-Installer", "1.0"));
        }
    }

    public async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(
        string owner = DefaultOwner, 
        string repo = DefaultRepo, 
        CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/releases";
            InstallerLogger.Info($"Fetching releases from GitHub: {url}");

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                InstallerLogger.Warn($"GitHub API returned status: {response.StatusCode}");
                return null;
            }

            var releases = await response.Content.ReadFromJsonAsync<List<GitHubRelease>>(cancellationToken: ct);
            if (releases == null || releases.Count == 0)
            {
                InstallerLogger.Warn("No releases returned from GitHub.");
                return null;
            }

            // Find the latest non-draft release
            var release = releases.FirstOrDefault(r => !r.Draft) ?? releases[0];
            InstallerLogger.Info($"Found release: Tag={release.TagName}, Name={release.Name}");

            // Find the best distribution asset (.rar, .zip, .7z)
            var asset = release.Assets.FirstOrDefault(a => 
                a.Name.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ||
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                a.Name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                ?? release.Assets.FirstOrDefault();

            if (asset == null)
            {
                InstallerLogger.Warn($"Release {release.TagName} has no downloadable distribution assets.");
                return null;
            }

            var cleanVersionText = release.TagName.Trim().TrimStart('v', 'V');
            var parsedVersion = AppVersionInfo.FromString(cleanVersionText);

            return new GitHubReleaseInfo
            {
                TagName = release.TagName,
                Version = parsedVersion,
                Title = string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name,
                ReleaseNotes = release.Body,
                PublishedAt = release.PublishedAt,
                DownloadUrl = asset.BrowserDownloadUrl,
                PackageFileName = asset.Name,
                PackageSizeBytes = asset.Size,
                HtmlUrl = release.HtmlUrl
            };
        }
        catch (Exception ex)
        {
            InstallerLogger.Error("Failed to fetch release info from GitHub", ex);
            return null;
        }
    }
}
