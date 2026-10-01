using System.IO;
using System.Reflection;
using System.Text.Json;

namespace OctalPulse.Application.Contracts;

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

    private static AppVersionInfo? _cachedCurrent;
    private static readonly object _versionLock = new();

    public static AppVersionInfo Current
    {
        get
        {
            if (_cachedCurrent != null)
                return _cachedCurrent;

            lock (_versionLock)
            {
                _cachedCurrent ??= ResolveCurrentVersion();
                return _cachedCurrent;
            }
        }
    }

    public static void InvalidateCache()
    {
        lock (_versionLock)
        {
            _cachedCurrent = null;
        }
    }

    private static AppVersionInfo ResolveCurrentVersion()
    {
        AppVersionInfo? fromInstallMeta = null;
        AppVersionInfo? fromAssembly = null;

        // 1. Try reading from installation.json (local app dir first, then LocalAppData\OctalPulse)
        try
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "installation.json"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OctalPulse", "installation.json")
            };

            foreach (var metaPath in candidates)
            {
                if (File.Exists(metaPath))
                {
                    var json = File.ReadAllText(metaPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Version", out var verProp) ||
                        doc.RootElement.TryGetProperty("version", out verProp))
                    {
                        var verStr = verProp.GetString();
                        if (!string.IsNullOrWhiteSpace(verStr))
                        {
                            var parsed = FromString(verStr);
                            if (parsed.Version > new Version(1, 0, 0, 0))
                            {
                                fromInstallMeta = parsed;
                                break;
                            }
                        }
                    }
                }
            }
        }
        catch { }

        // 2. Try reading from Entry Assembly / Executing Assembly
        try
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var infoVerAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (!string.IsNullOrWhiteSpace(infoVerAttr?.InformationalVersion))
            {
                var raw = infoVerAttr.InformationalVersion.Split('+')[0];
                var parsed = FromString(raw);
                if (parsed.Version > new Version(1, 0, 0, 0))
                {
                    fromAssembly = parsed;
                }
            }

            if (fromAssembly == null)
            {
                var ver = asm.GetName().Version;
                if (ver != null && ver > new Version(1, 0, 0, 0))
                {
                    fromAssembly = new AppVersionInfo(ver);
                }
            }
        }
        catch { }

        // Pick highest valid version detected
        if (fromInstallMeta != null && fromAssembly != null)
        {
            return fromAssembly.CompareTo(fromInstallMeta) > 0 ? fromAssembly : fromInstallMeta;
        }

        if (fromInstallMeta != null)
            return fromInstallMeta;

        if (fromAssembly != null)
            return fromAssembly;

        return new AppVersionInfo(new Version(1, 0, 0, 0));
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
