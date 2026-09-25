using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using OctalPulse.Installer.Models;

namespace OctalPulse.Installer.Services;

public class InstallationManager
{
    private readonly HttpClient _httpClient;

    public const string DefaultManifestUrl = "https://raw.githubusercontent.com/octal811/OctalTeammate/main/releases/update.json";
    public const string InstallationMetaFileName = "installation.json";
    public const string MainExecutableName = "OctalPulse.exe";
    public const string InstallerExecutableName = "OctalPulse.Installer.exe";

    public InstallationManager(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("OctalPulse-Installer/1.0");
    }

    public static string GetDefaultInstallDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "OctalPulse");
    }

    public static string GetTempUpdateDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "OctalPulse", "UpdateTemp");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public InstallationMetadata? ReadInstallationMetadata(string installDir)
    {
        try
        {
            var metaPath = Path.Combine(installDir, InstallationMetaFileName);
            if (!File.Exists(metaPath))
            {
                // Also check parent/global %LocalAppData%\OctalPulse if installDir is different
                var globalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OctalPulse", InstallationMetaFileName);
                if (File.Exists(globalPath))
                    metaPath = globalPath;
                else
                    return null;
            }

            var json = File.ReadAllText(metaPath);
            return JsonSerializer.Deserialize<InstallationMetadata>(json);
        }
        catch (Exception ex)
        {
            InstallerLogger.Warn($"Could not read installation metadata from {installDir}: {ex.Message}");
            return null;
        }
    }

    public void WriteInstallationMetadata(string installDir, InstallationMetadata metadata)
    {
        try
        {
            Directory.CreateDirectory(installDir);
            var metaPath = Path.Combine(installDir, InstallationMetaFileName);
            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metaPath, json);

            // Also keep a copy at the standard %LocalAppData%\OctalPulse root if installed in custom path
            var globalDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OctalPulse");
            Directory.CreateDirectory(globalDir);
            var globalPath = Path.Combine(globalDir, InstallationMetaFileName);
            File.WriteAllText(globalPath, json);
        }
        catch (Exception ex)
        {
            InstallerLogger.Error($"Failed to write installation metadata to {installDir}", ex);
        }
    }

    public async Task<UpdateManifest?> FetchUpdateManifestAsync(string manifestUrl, CancellationToken ct = default)
    {
        InstallerLogger.Info($"Fetching update manifest from {manifestUrl}");
        using var response = await _httpClient.GetAsync(manifestUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var manifest = await response.Content.ReadFromJsonAsync<UpdateManifest>(cancellationToken: ct);
        return manifest;
    }

    public async Task<bool> WaitForApplicationExitAsync(TimeSpan timeout, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        InstallerLogger.Info("Waiting for OctalPulse process to exit...");
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            ct.ThrowIfCancellationRequested();
            var processes = Process.GetProcessesByName("OctalPulse");
            if (processes.Length == 0)
            {
                InstallerLogger.Info("Application closed.");
                return true;
            }

            progress?.Report($"Waiting for OctalPulse to exit ({processes.Length} process active)...");
            await Task.Delay(500, ct);
        }

        // If still alive after timeout, check once more
        var remaining = Process.GetProcessesByName("OctalPulse");
        if (remaining.Length == 0) return true;

        InstallerLogger.Warn("OctalPulse process did not exit within the timeout window.");
        return false;
    }

    public async Task DownloadPackageAsync(
        string downloadUrl,
        string targetZipPath,
        IProgress<(double percent, long downloadedBytes, long totalBytes)>? progress = null,
        CancellationToken ct = default)
    {
        InstallerLogger.Info($"Download started: {downloadUrl} -> {targetZipPath}");

        // Ensure temp directory exists and target file is clean
        var parentDir = Path.GetDirectoryName(targetZipPath);
        if (!string.IsNullOrEmpty(parentDir)) Directory.CreateDirectory(parentDir);

        if (File.Exists(targetZipPath))
        {
            try { File.Delete(targetZipPath); } catch { }
        }

        try
        {
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(targetZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    double pct = (double)totalRead / totalBytes * 100.0;
                    progress?.Report((pct, totalRead, totalBytes));
                }
                else
                {
                    progress?.Report((-1, totalRead, -1));
                }
            }

            InstallerLogger.Info($"Download completed successfully. {totalRead} bytes written to {targetZipPath}.");
        }
        catch
        {
            // If download fails or cancelled, delete partial file so old version is untouched
            if (File.Exists(targetZipPath))
            {
                try { File.Delete(targetZipPath); } catch { }
            }
            throw;
        }
    }

    public async Task ExtractPackageAsync(string zipPath, string targetExtractedDir, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        InstallerLogger.Info($"Extraction started: {zipPath} -> {targetExtractedDir}");

        if (!File.Exists(zipPath))
            throw new FileNotFoundException($"Package ZIP file not found: {zipPath}");

        if (Directory.Exists(targetExtractedDir))
        {
            try { Directory.Delete(targetExtractedDir, true); } catch { }
        }
        Directory.CreateDirectory(targetExtractedDir);

        await Task.Run(() =>
        {
            progress?.Report("Extracting package archive...");
            ZipFile.ExtractToDirectory(zipPath, targetExtractedDir, overwriteFiles: true);
        }, ct);

        InstallerLogger.Info("Extraction completed.");
    }

    public async Task MergeAndCleanAsync(
        string extractedDir,
        string installDir,
        string newVersion,
        IProgress<(string status, double percent)>? progress = null,
        CancellationToken ct = default)
    {
        InstallerLogger.Info($"Merge started: from {extractedDir} to {installDir}");

        Directory.CreateDirectory(installDir);

        // 1. Read existing metadata to determine obsolete files later
        var previousMeta = ReadInstallationMetadata(installDir);
        var previousFiles = new HashSet<string>(previousMeta?.InstalledFiles ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

        // 2. Mark installation incomplete before starting merge (interrupted update detection)
        var stagingMeta = new InstallationMetadata
        {
            Version = newVersion,
            InstallPath = installDir,
            Installed = false,
            InstalledAt = DateTime.UtcNow,
            InstalledFiles = previousMeta?.InstalledFiles ?? new List<string>()
        };
        WriteInstallationMetadata(installDir, stagingMeta);

        // 3. Resolve the actual root of extracted files (handles case where ZIP contains a single root folder)
        var sourceDir = ResolvePayloadRoot(extractedDir);

        var allExtractedFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        var newInstalledFiles = new List<string>();

        progress?.Report(("Merging and updating application files...", 0));

        // 4. Copy each extracted file to destination
        for (int i = 0; i < allExtractedFiles.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var srcFile = allExtractedFiles[i];
            var relativePath = Path.GetRelativePath(sourceDir, srcFile);
            var destFile = Path.Combine(installDir, relativePath);

            var destFolder = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }

            File.Copy(srcFile, destFile, overwrite: true);
            newInstalledFiles.Add(relativePath);

            double pct = (double)(i + 1) / allExtractedFiles.Length * 80.0;
            progress?.Report(($"Updating files ({i + 1}/{allExtractedFiles.Length})...", pct));
        }

        // Also copy OctalPulse.Installer.exe into install directory so it is always present alongside the app
        CopyCurrentInstallerToInstallDir(installDir);

        // 5. Obsolete file removal
        progress?.Report(("Removing obsolete files from previous version...", 85));
        var newFilesSet = new HashSet<string>(newInstalledFiles, StringComparer.OrdinalIgnoreCase);

        foreach (var oldRelative in previousFiles)
        {
            if (!newFilesSet.Contains(oldRelative))
            {
                if (IsProtectedFile(oldRelative))
                {
                    continue;
                }

                var oldFullPath = Path.Combine(installDir, oldRelative);
                if (File.Exists(oldFullPath))
                {
                    try
                    {
                        File.Delete(oldFullPath);
                        InstallerLogger.Info($"Removed obsolete file: {oldRelative}");
                    }
                    catch (Exception ex)
                    {
                        InstallerLogger.Warn($"Could not remove obsolete file {oldRelative}: {ex.Message}");
                    }
                }
            }
        }

        // Clean up empty directories
        CleanEmptyDirectories(installDir);

        // 6. Mark installation as successfully completed!
        var completeMeta = new InstallationMetadata
        {
            Version = newVersion,
            InstallPath = installDir,
            Installed = true,
            InstalledAt = DateTime.UtcNow,
            InstalledFiles = newInstalledFiles
        };
        WriteInstallationMetadata(installDir, completeMeta);

        progress?.Report(("Installation completed successfully.", 100));
        InstallerLogger.Info("Merge completed. Installation marked valid.");
    }

    private string ResolvePayloadRoot(string extractedDir)
    {
        // If extractedDir contains a single directory and no loose files, descend into it
        var topFiles = Directory.GetFiles(extractedDir);
        var topDirs = Directory.GetDirectories(extractedDir);

        if (topFiles.Length == 0 && topDirs.Length == 1)
        {
            return topDirs[0];
        }

        return extractedDir;
    }

    public void CopyCurrentInstallerToInstallDir(string installDir)
    {
        try
        {
            var currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe))
            {
                var targetExe = Path.Combine(installDir, InstallerExecutableName);
                if (!string.Equals(Path.GetFullPath(currentExe), Path.GetFullPath(targetExe), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(currentExe, targetExe, overwrite: true);
                    InstallerLogger.Info($"Copied Installer to: {targetExe}");
                }
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.Warn($"Could not copy installer to destination directory: {ex.Message}");
        }
    }

    private bool IsProtectedFile(string relativePath)
    {
        var name = Path.GetFileName(relativePath);
        var ext = Path.GetExtension(relativePath).ToLowerInvariant();

        // Never delete installer executable
        if (name.Equals(InstallerExecutableName, StringComparison.OrdinalIgnoreCase))
            return true;

        // Never delete databases
        if (ext.Contains("db") || name.StartsWith("octal_desktop", StringComparison.OrdinalIgnoreCase))
            return true;

        // Never delete user credentials / secure storage
        if (name.Contains("SecureStore", StringComparison.OrdinalIgnoreCase) || relativePath.Contains("SecureStore", StringComparison.OrdinalIgnoreCase))
            return true;

        // Never delete logs or installation metadata
        if (ext == ".log" || name.Equals(InstallationMetaFileName, StringComparison.OrdinalIgnoreCase))
            return true;

        // Never delete user settings
        if (name.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private void CleanEmptyDirectories(string startLocation)
    {
        try
        {
            foreach (var directory in Directory.GetDirectories(startLocation))
            {
                CleanEmptyDirectories(directory);
                if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                {
                    try { Directory.Delete(directory, false); } catch { }
                }
            }
        }
        catch { }
    }

    public void CleanupTemp(string tempDir)
    {
        try
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
                InstallerLogger.Info($"Cleaned up temporary directory: {tempDir}");
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.Warn($"Could not fully clean temp directory {tempDir}: {ex.Message}");
        }
    }

    public bool LaunchApplication(string installDir)
    {
        var exePath = Path.Combine(installDir, MainExecutableName);
        if (!File.Exists(exePath))
        {
            InstallerLogger.Error($"Cannot start application: {exePath} not found.");
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = installDir,
                UseShellExecute = true
            };
            Process.Start(psi);
            InstallerLogger.Info($"Application launched: {exePath}");
            return true;
        }
        catch (Exception ex)
        {
            InstallerLogger.Error($"Failed to launch application at {exePath}", ex);
            return false;
        }
    }
}
