using System.Reflection;

namespace OctalPulse.Installer.Models;

/// <summary>
/// Represents the 4-part application version:
/// X1: Backend provider update (Major)
/// X2: New desktop feature (Minor)
/// X3: UI / UX or theme addition (Build)
/// X4: Bug or error fix (Revision)
/// </summary>
public sealed class AppVersionInfo : IComparable<AppVersionInfo>
{
    public Version Version { get; }

    public int BackendProvider => Math.Max(0, Version.Major);
    public int DesktopFeatures => Math.Max(0, Version.Minor);
    public int UiUxTheme => Math.Max(0, Version.Build);
    public int BugFixes => Math.Max(0, Version.Revision);

    public string DisplayString => $"{BackendProvider}.{DesktopFeatures}.{UiUxTheme}.{BugFixes}";

    public AppVersionInfo(Version version)
    {
        Version = NormalizeVersion(version);
    }

    public static AppVersionInfo FromString(string? versionText)
    {
        if (string.IsNullOrWhiteSpace(versionText))
            return new AppVersionInfo(new Version(1, 0, 0, 0));

        var clean = versionText.Trim().TrimStart('v', 'V');
        if (Version.TryParse(clean, out var parsed))
        {
            return new AppVersionInfo(parsed);
        }

        // Handle standard 3-part or 2-part by padding
        var parts = clean.Split('.');
        int major = parts.Length > 0 && int.TryParse(parts[0], out var p0) ? p0 : 1;
        int minor = parts.Length > 1 && int.TryParse(parts[1], out var p1) ? p1 : 0;
        int build = parts.Length > 2 && int.TryParse(parts[2], out var p2) ? p2 : 0;
        int rev = parts.Length > 3 && int.TryParse(parts[3], out var p3) ? p3 : 0;

        return new AppVersionInfo(new Version(major, minor, build, rev));
    }

    public static AppVersionInfo Current
    {
        get
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;
            return ver != null ? new AppVersionInfo(ver) : new AppVersionInfo(new Version(1, 0, 0, 0));
        }
    }

    public static Version NormalizeVersion(Version v)
    {
        int major = Math.Max(0, v.Major);
        int minor = Math.Max(0, v.Minor);
        int build = Math.Max(0, v.Build);
        int revision = Math.Max(0, v.Revision);
        return new Version(major, minor, build, revision);
    }

    public int CompareTo(AppVersionInfo? other)
    {
        if (other is null) return 1;
        return Version.CompareTo(other.Version);
    }

    public override string ToString() => DisplayString;
}

public sealed class UpdateManifest
{
    public string Version { get; set; } = "1.0.0.0";
    public string DownloadUrl { get; set; } = string.Empty;
    public string? ReleaseNotes { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public List<string>? Files { get; set; }
}

public sealed class UpdateCheckResult
{
    public bool IsUpdateAvailable { get; set; }
    public AppVersionInfo CurrentVersion { get; set; } = AppVersionInfo.Current;
    public AppVersionInfo? NewVersion { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
}

public sealed class InstallationMetadata
{
    public string Version { get; set; } = "1.0.0.0";
    public string InstallPath { get; set; } = string.Empty;
    public bool Installed { get; set; }
    public DateTime? InstalledAt { get; set; }
    public List<string> InstalledFiles { get; set; } = new();
}

public sealed class GitHubRelease
{
    [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("body")]
    public string? Body { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();
}

public sealed class GitHubAsset
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("size")]
    public long Size { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("content_type")]
    public string? ContentType { get; set; }
}

