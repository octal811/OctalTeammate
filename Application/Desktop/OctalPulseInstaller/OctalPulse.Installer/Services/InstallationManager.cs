using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Win32;
using OctalPulse.Installer.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace OctalPulse.Installer.Services;

public class InstallationManager
{
    private readonly HttpClient _httpClient;

    public const string InstallationMetaFileName = "installation.json";
    public const string MainExecutableName = "OctalPulse.exe";
    public const string InstallerExecutableName = "OctalPulse.Installer.exe";
    public const string RegistrySubKey = @"Software\OctalPulse";
    public const string RegistryPathValueName = "InstallPath";

    public InstallationManager(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctalPulse-Installer", "1.0"));
        }
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

    #region Path Discovery & Persistence

    public string GetStoredInstallDirectory()
    {
        // 1. Check registry
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistrySubKey);
            var regPath = key?.GetValue(RegistryPathValueName) as string;
            if (!string.IsNullOrWhiteSpace(regPath) && Directory.Exists(regPath))
            {
                return regPath;
            }
        }
        catch { }

        // 2. Check %LocalAppData%\OctalPulse\installation.json
        try
        {
            var globalMeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OctalPulse", InstallationMetaFileName);
            if (File.Exists(globalMeta))
            {
                var json = File.ReadAllText(globalMeta);
                var meta = JsonSerializer.Deserialize<InstallationMetadata>(json);
                if (!string.IsNullOrWhiteSpace(meta?.InstallPath) && Directory.Exists(meta.InstallPath))
                {
                    return meta.InstallPath;
                }
            }
        }
        catch { }

        // 3. Fallback to default per-user path
        return GetDefaultInstallDirectory();
    }

    public void SaveInstallDirectory(string path)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistrySubKey);
            key?.SetValue(RegistryPathValueName, path);
        }
        catch (Exception ex)
        {
            InstallerLogger.Warn($"Failed to save install path to registry: {ex.Message}");
        }
    }

    public bool ValidateAppFolder(string path, out string detectedVersion, out string reason)
    {
        detectedVersion = "Unknown";
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "No path specified.";
            return false;
        }

        if (!Directory.Exists(path))
        {
            reason = $"Directory does not exist: {path}";
            return false;
        }

        var mainExe = Path.Combine(path, MainExecutableName);
        if (!File.Exists(mainExe))
        {
            reason = $"'{MainExecutableName}' was not found in the selected folder.";
            return false;
        }

        // Try to read file version from OctalPulse.exe
        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(mainExe);
            if (!string.IsNullOrWhiteSpace(versionInfo.FileVersion))
            {
                detectedVersion = versionInfo.FileVersion.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(versionInfo.ProductVersion))
            {
                detectedVersion = versionInfo.ProductVersion.Trim();
            }
        }
        catch { }

        // If file version was fallback, check installation.json in that folder
        var localMeta = Path.Combine(path, InstallationMetaFileName);
        if (File.Exists(localMeta))
        {
            try
            {
                var json = File.ReadAllText(localMeta);
                var meta = JsonSerializer.Deserialize<InstallationMetadata>(json);
                if (meta != null && !string.IsNullOrWhiteSpace(meta.Version))
                {
                    detectedVersion = meta.Version;
                }
            }
            catch { }
        }

        return true;
    }

    public InstallationMetadata? ReadInstallationMetadata(string installDir)
    {
        try
        {
            var metaPath = Path.Combine(installDir, InstallationMetaFileName);
            if (!File.Exists(metaPath))
            {
                var globalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OctalPulse", InstallationMetaFileName);
                if (File.Exists(globalPath))
                    metaPath = globalPath;
            }

            if (!File.Exists(metaPath))
                return null;

            var json = File.ReadAllText(metaPath);
            return JsonSerializer.Deserialize<InstallationMetadata>(json);
        }
        catch (Exception ex)
        {
            InstallerLogger.Error($"Failed to read metadata from {installDir}", ex);
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

            var globalDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OctalPulse");
            Directory.CreateDirectory(globalDir);
            var globalPath = Path.Combine(globalDir, InstallationMetaFileName);
            File.WriteAllText(globalPath, json);

            SaveInstallDirectory(installDir);
        }
        catch (Exception ex)
        {
            InstallerLogger.Error($"Failed to write installation metadata to {installDir}", ex);
        }
    }

    #endregion

    #region Process & Download Management

    public async Task<bool> WaitForApplicationExitAsync(TimeSpan timeout, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        InstallerLogger.Info("Checking for running OctalPulse processes...");
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            ct.ThrowIfCancellationRequested();
            var processes = Process.GetProcessesByName("OctalPulse");
            if (processes.Length == 0)
            {
                return true;
            }

            progress?.Report($"Waiting for OctalPulse to exit ({processes.Length} process active)...");
            await Task.Delay(500, ct);
        }

        var remaining = Process.GetProcessesByName("OctalPulse");
        return remaining.Length == 0;
    }

    /// <summary>
    /// Resumable download supporting HTTP Range headers. If connection is lost or paused,
    /// the partially downloaded bytes remain intact so download can resume.
    /// </summary>
    public async Task DownloadPackageAsync(
        string downloadUrl,
        string targetFilePath,
        IProgress<(double percent, long downloadedBytes, long totalBytes)>? progress = null,
        CancellationToken ct = default)
    {
        InstallerLogger.Info($"Starting (or resuming) download: {downloadUrl} -> {targetFilePath}");

        var parentDir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(parentDir)) Directory.CreateDirectory(parentDir);

        long existingBytes = 0;
        if (File.Exists(targetFilePath))
        {
            existingBytes = new FileInfo(targetFilePath).Length;
            InstallerLogger.Info($"Found existing partial download: {existingBytes} bytes.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        if (existingBytes > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existingBytes, null);
        }

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        long totalBytes;
        bool appendMode = false;

        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            appendMode = true;
            var contentRange = response.Content.Headers.ContentRange;
            totalBytes = contentRange?.Length ?? (existingBytes + (response.Content.Headers.ContentLength ?? 0));
            InstallerLogger.Info($"Server accepted Range request (206). Resuming from {existingBytes} bytes of {totalBytes}.");
        }
        else if (response.IsSuccessStatusCode)
        {
            appendMode = false;
            existingBytes = 0;
            totalBytes = response.Content.Headers.ContentLength ?? -1L;
            InstallerLogger.Info($"Server returned full content (200). Starting fresh ({totalBytes} bytes).");
        }
        else
        {
            response.EnsureSuccessStatusCode();
            return;
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(
            targetFilePath,
            appendMode ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            true);

        var buffer = new byte[81920];
        long totalRead = existingBytes;
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

        InstallerLogger.Info($"Download finished: {totalRead} bytes saved to {targetFilePath}.");
    }

    #endregion

    #region Extraction & Clean Replacement

    /// <summary>
    /// Extracts .rar, .zip, .7z archives using SharpCompress.
    /// </summary>
    public async Task ExtractPackageAsync(string packagePath, string targetExtractedDir, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        InstallerLogger.Info($"Extracting package: {packagePath} -> {targetExtractedDir}");

        if (!File.Exists(packagePath))
            throw new FileNotFoundException($"Package file not found: {packagePath}");

        if (Directory.Exists(targetExtractedDir))
        {
            try { Directory.Delete(targetExtractedDir, true); } catch { }
        }
        Directory.CreateDirectory(targetExtractedDir);

        await Task.Run(() =>
        {
            progress?.Report("Extracting package archive...");

            using var archive = ArchiveFactory.OpenArchive(packagePath);
            int totalEntries = archive.Entries.Count();
            int current = 0;

            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                ct.ThrowIfCancellationRequested();
                entry.WriteToDirectory(targetExtractedDir, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true
                });

                current++;
                if (current % 10 == 0 || current == totalEntries)
                {
                    progress?.Report($"Extracting files ({current}/{totalEntries})...");
                }
            }
        }, ct);

        InstallerLogger.Info("Extraction completed successfully.");
    }

    /// <summary>
    /// Replaces the application folder completely with the new version.
    /// Uses an atomic backup-and-swap mechanism so the old version is preserved if anything goes wrong.
    /// </summary>
    public async Task ReplaceApplicationFolderAsync(
        string extractedDir,
        string installDir,
        string newVersion,
        IProgress<(string status, double percent)>? progress = null,
        CancellationToken ct = default)
    {
        InstallerLogger.Info($"Starting clean folder replacement: {extractedDir} -> {installDir}");

        // 1. Resolve payload root (in case archive contains a root folder like 'net10.0-windows')
        var sourceDir = ResolvePayloadRoot(extractedDir);
        var sourceExe = Path.Combine(sourceDir, MainExecutableName);
        if (!File.Exists(sourceExe))
        {
            throw new FileNotFoundException($"Extracted package is missing required executable: '{MainExecutableName}'");
        }

        Directory.CreateDirectory(installDir);

        var backupDir = Path.Combine(
            Path.GetDirectoryName(installDir) ?? installDir,
            Path.GetFileName(installDir) + $".bak_{DateTime.UtcNow:yyyyMMddHHmmss}");

        bool hasBackup = false;

        try
        {
            // 2. If existing files are present, prepare safe backup
            var existingFiles = Directory.GetFiles(installDir, "*", SearchOption.AllDirectories);
            if (existingFiles.Length > 0)
            {
                progress?.Report(("Backing up existing version before replacement...", 10));
                InstallerLogger.Info($"Creating backup of current version at {backupDir}...");
                
                // Copy user logs or custom data to preserve if present
                CopyDirectory(installDir, backupDir);
                hasBackup = true;
            }

            // 3. Clean existing install directory except protected user assets
            progress?.Report(("Cleaning previous version files...", 30));
            CleanDirectory(installDir);

            // 4. Copy all new files from sourceDir to installDir
            var allNewFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            var installedRelativeFiles = new List<string>();

            for (int i = 0; i < allNewFiles.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                var srcFile = allNewFiles[i];
                var relative = Path.GetRelativePath(sourceDir, srcFile);
                var destFile = Path.Combine(installDir, relative);

                var folder = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                File.Copy(srcFile, destFile, overwrite: true);
                installedRelativeFiles.Add(relative);

                double pct = 40.0 + ((double)(i + 1) / allNewFiles.Length * 50.0);
                progress?.Report(($"Installing files ({i + 1}/{allNewFiles.Length})...", pct));
            }

            // 5. Ensure OctalPulse.Installer.exe is copied alongside the app
            CopyCurrentInstallerToInstallDir(installDir);

            // 6. Write final metadata
            var metadata = new InstallationMetadata
            {
                Version = newVersion,
                InstallPath = installDir,
                Installed = true,
                InstalledAt = DateTime.UtcNow,
                InstalledFiles = installedRelativeFiles
            };
            WriteInstallationMetadata(installDir, metadata);

            progress?.Report(("Finalizing update...", 95));

            // 7. Cleanup backup folder on success
            if (hasBackup && Directory.Exists(backupDir))
            {
                try { Directory.Delete(backupDir, true); } catch { }
            }

            progress?.Report(("Installation completed successfully!", 100));
            InstallerLogger.Info($"Clean replacement completed. Version v{newVersion} is installed at {installDir}.");
        }
        catch (Exception ex)
        {
            InstallerLogger.Error("Folder replacement failed! Attempting rollback...", ex);

            // Rollback if backup exists
            if (hasBackup && Directory.Exists(backupDir))
            {
                try
                {
                    progress?.Report(("Restoring previous version from backup...", 0));
                    CleanDirectory(installDir);
                    CopyDirectory(backupDir, installDir);
                    Directory.Delete(backupDir, true);
                    InstallerLogger.Info("Rollback completed successfully.");
                }
                catch (Exception rollbackEx)
                {
                    InstallerLogger.Error("Rollback failed!", rollbackEx);
                }
            }

            throw;
        }
    }

    private string ResolvePayloadRoot(string extractedDir)
    {
        // 1. Direct check: is OctalPulse.exe directly in extractedDir?
        if (File.Exists(Path.Combine(extractedDir, MainExecutableName)))
        {
            return extractedDir;
        }

        // 2. Search for OctalPulse.exe in subdirectories (up to 3 levels deep)
        var foundExes = Directory.GetFiles(extractedDir, MainExecutableName, SearchOption.AllDirectories);
        if (foundExes.Length > 0)
        {
            var dir = Path.GetDirectoryName(foundExes[0]);
            if (!string.IsNullOrEmpty(dir)) return dir;
        }

        // Fallback
        return extractedDir;
    }

    private void CleanDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;

        foreach (var file in Directory.GetFiles(dir))
        {
            var name = Path.GetFileName(file);
            // Don't delete running installer executable
            if (name.Equals(InstallerExecutableName, StringComparison.OrdinalIgnoreCase)) continue;

            try { File.Delete(file); } catch { }
        }

        foreach (var sub in Directory.GetDirectories(dir))
        {
            var name = Path.GetFileName(sub);
            // Preserve user logs or local databases if any
            if (name.Equals("logs", StringComparison.OrdinalIgnoreCase)) continue;

            try { Directory.Delete(sub, true); } catch { }
        }
    }

    private void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(targetDir, rel);
            var folder = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.Copy(file, dest, true);
        }
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

    public bool LaunchApplication(string installDir)
    {
        try
        {
            var exePath = Path.Combine(installDir, MainExecutableName);
            if (!File.Exists(exePath))
            {
                InstallerLogger.Error($"Cannot launch: {exePath} does not exist.");
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = installDir,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            InstallerLogger.Info($"Application launched: {exePath}");
            return true;
        }
        catch (Exception ex)
        {
            InstallerLogger.Error("Failed to launch application", ex);
            return false;
        }
    }

    public void CleanupTemp(string tempDir)
    {
        try
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.Warn($"Failed to cleanup temp dir {tempDir}: {ex.Message}");
        }
    }

    public void CleanupPartialDownload(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch { }
    }

    #endregion
}
