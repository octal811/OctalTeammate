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
        return Path.Combine(appData, "Programs", "OctalPulse");
    }

    public static string GetTempUpdateDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "OctalPulseUpdate");
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

    public async Task<bool> CloseAndTerminateApplicationProcessesAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        InstallerLogger.Info("Checking for running OctalPulse processes...");
        var processes = Process.GetProcessesByName("OctalPulse");
        if (processes.Length == 0) return true;

        progress?.Report("Closing running OctalPulse application...");
        InstallerLogger.Info($"Found {processes.Length} running OctalPulse process(es). Requesting graceful exit...");

        // 1. Request graceful close of main window
        foreach (var p in processes)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.CloseMainWindow();
                }
            }
            catch { }
        }

        // Wait up to 3 seconds for graceful shutdown
        for (int i = 0; i < 6; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(500, ct);
            if (Process.GetProcessesByName("OctalPulse").Length == 0)
            {
                InstallerLogger.Info("OctalPulse exited gracefully.");
                return true;
            }
        }

        // 2. Force terminate if still running in background / tray
        var remaining = Process.GetProcessesByName("OctalPulse");
        if (remaining.Length > 0)
        {
            progress?.Report("Terminating background OctalPulse process...");
            InstallerLogger.Warn($"Force terminating {remaining.Length} background OctalPulse process(es)...");

            foreach (var p in remaining)
            {
                try
                {
                    if (!p.HasExited)
                    {
                        p.Kill(true);
                        p.WaitForExit(3000);
                    }
                }
                catch (Exception ex)
                {
                    InstallerLogger.Warn($"Failed to kill process {p.Id}: {ex.Message}");
                }
            }
        }

        await Task.Delay(500, ct);
        var finalCheck = Process.GetProcessesByName("OctalPulse");
        return finalCheck.Length == 0;
    }

    public async Task<bool> WaitForApplicationExitAsync(TimeSpan timeout, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        return await CloseAndTerminateApplicationProcessesAsync(progress, ct);
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
                var entryKey = entry.Key ?? string.Empty;
                var fileName = Path.GetFileName(entryKey);
                // Exclude any legacy installer binaries that were packaged inside older release archives
                if (fileName.StartsWith("OctalPulse.Installer.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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

            // 5. Clean up any legacy installer binaries from the app folder
            CleanupLegacyInstallerFiles(installDir);

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

            // 7. Ensure a persistent copy of the installer is available in global AppData
            CopyInstallerToGlobalDirectory();

            // 8. Create Start Menu and Desktop shortcuts, and register application
            CreateShortcutsAndRegister(installDir, newVersion);

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

        var currentExe = Environment.ProcessPath ?? string.Empty;
        var tempDir = GetTempUpdateDirectory();

        foreach (var file in Directory.GetFiles(dir))
        {
            // Only preserve if this file is the currently running process itself
            if (!string.IsNullOrEmpty(currentExe) &&
                string.Equals(Path.GetFullPath(file), Path.GetFullPath(currentExe), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = Path.GetFileName(file);
            // Protect user database, configurations, or logs if present
            if (fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("float_positions.json", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("startup_debug.log", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try { File.Delete(file); } catch { }
        }

        foreach (var sub in Directory.GetDirectories(dir))
        {
            var name = Path.GetFileName(sub);
            // Preserve user logs, SecureStore, or any UpdateTemp folder
            if (name.Equals("logs", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("SecureStore", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("UpdateTemp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Never delete the temporary working directory or its parents/children
            if (IsSameOrSubdirectory(sub, tempDir))
            {
                continue;
            }

            try { Directory.Delete(sub, true); } catch { }
        }
    }

    private void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, file);
            if (rel.StartsWith("logs" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                rel.StartsWith("UpdateTemp" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dest = Path.Combine(targetDir, rel);
            var folder = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.Copy(file, dest, true);
        }
    }

    private static bool IsSameOrSubdirectory(string candidatePath, string basePath)
    {
        try
        {
            var fullCandidate = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullBase = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return fullCandidate.Equals(fullBase, StringComparison.OrdinalIgnoreCase) ||
                   fullBase.StartsWith(fullCandidate + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                   fullCandidate.StartsWith(fullBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void CopyInstallerToGlobalDirectory()
    {
        try
        {
            var currentExe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe))
            {
                var globalDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OctalPulse");
                Directory.CreateDirectory(globalDir);
                var targetPath = Path.Combine(globalDir, InstallerExecutableName);
                if (!string.Equals(Path.GetFullPath(currentExe), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(currentExe, targetPath, overwrite: true);
                    InstallerLogger.Info($"Copied installer to global directory: {targetPath}");
                }
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.Warn($"Failed to copy installer to global directory: {ex.Message}");
        }
    }

    public static void CreateShortcutsAndRegister(string installDir, string version)
    {
        try
        {
            var targetExe = Path.Combine(installDir, MainExecutableName);
            if (!File.Exists(targetExe)) return;

            // 1. Start Menu Shortcut (overwrites existing OctalPulse.lnk in place, no duplicate)
            var startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            if (Directory.Exists(startMenuDir))
            {
                var startMenuPath = Path.Combine(startMenuDir, "OctalPulse.lnk");
                CleanDuplicateShortcuts(startMenuDir, startMenuPath);
                CreateShortcut(startMenuPath, targetExe, installDir, "OctalPulse Desktop Application");
            }

            // 2. Desktop Shortcut (overwrites existing OctalPulse.lnk in place, no duplicate)
            var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (Directory.Exists(desktopDir))
            {
                var desktopPath = Path.Combine(desktopDir, "OctalPulse.lnk");
                CleanDuplicateShortcuts(desktopDir, desktopPath);
                CreateShortcut(desktopPath, targetExe, installDir, "OctalPulse Desktop Application");
            }

            // 3. Register in Windows Programs & Features
            RegisterWindowsUninstall(installDir, targetExe, version);
        }
        catch (Exception ex)
        {
            InstallerLogger.Warn($"Failed to create shortcuts or register app: {ex.Message}");
        }
    }

    private static void CleanDuplicateShortcuts(string directory, string primaryShortcutPath)
    {
        try
        {
            if (!Directory.Exists(directory)) return;

            var primaryName = Path.GetFileName(primaryShortcutPath);
            foreach (var file in Directory.GetFiles(directory, "OctalPulse*.lnk"))
            {
                var fileName = Path.GetFileName(file);
                // Clean any stray duplicates like "OctalPulse - Shortcut.lnk", "OctalPulse (1).lnk", etc.
                if (!string.Equals(fileName, primaryName, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(file);
                        InstallerLogger.Info($"Removed duplicate shortcut: {file}");
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    public static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description = "")
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDir;
            shortcut.Description = description;
            shortcut.IconLocation = targetPath + ",0";
            shortcut.Save();

            InstallerLogger.Info($"Shortcut created successfully: {shortcutPath}");
        }
        catch (Exception ex)
        {
            InstallerLogger.Warn($"Failed to create shortcut at {shortcutPath}: {ex.Message}");
        }
    }

    public static void RegisterWindowsUninstall(string installDir, string targetExe, string version)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\OctalPulse");
            if (key != null)
            {
                key.SetValue("DisplayName", "OctalPulse");
                key.SetValue("DisplayVersion", version);
                key.SetValue("DisplayIcon", targetExe + ",0");
                key.SetValue("Publisher", "OctalPulse");
                key.SetValue("InstallLocation", installDir);
                var installerGlobal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OctalPulse", InstallerExecutableName);
                if (File.Exists(installerGlobal))
                {
                    key.SetValue("UninstallString", $"\"{installerGlobal}\" --uninstall");
                    key.SetValue("QuietUninstallString", $"\"{installerGlobal}\" --uninstall");
                }
                else
                {
                    key.SetValue("UninstallString", $"\"{targetExe}\" --uninstall");
                }
                key.SetValue("NoRepair", 1);
                key.SetValue("NoModify", 1);
                InstallerLogger.Info("Windows Uninstall registry entry updated successfully.");
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.Warn($"Failed to register in Windows Uninstall registry: {ex.Message}");
        }
    }

    public static void CleanupLegacyInstallerFiles(string installDir)
    {
        try
        {
            if (!Directory.Exists(installDir)) return;
            var currentExe = Environment.ProcessPath ?? string.Empty;

            foreach (var file in Directory.GetFiles(installDir, "OctalPulse.Installer.*"))
            {
                if (!string.IsNullOrEmpty(currentExe) &&
                    string.Equals(Path.GetFullPath(file), Path.GetFullPath(currentExe), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    File.Delete(file);
                    InstallerLogger.Info($"Removed legacy installer binary: {file}");
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.Warn($"Failed to cleanup legacy installer files: {ex.Message}");
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

    public async Task<bool> UninstallApplicationAsync(
        string installDir,
        bool removeUserData,
        IProgress<(string status, double percent)>? progress = null,
        CancellationToken ct = default)
    {
        InstallerLogger.Info($"Starting uninstallation of OctalPulse. InstallDir={installDir}, RemoveUserData={removeUserData}");

        try
        {
            // 1. Force close and terminate any running OctalPulse processes
            progress?.Report(("Closing running OctalPulse processes...", 15));
            var processProgress = new Progress<string>(s => progress?.Report((s, 20)));
            await CloseAndTerminateApplicationProcessesAsync(processProgress, ct);

            // 2. Remove Shortcuts
            progress?.Report(("Removing shortcuts...", 40));
            try
            {
                var startMenuShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "OctalPulse.lnk");
                if (File.Exists(startMenuShortcut))
                {
                    File.Delete(startMenuShortcut);
                    InstallerLogger.Info($"Deleted Start Menu shortcut: {startMenuShortcut}");
                }

                var desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "OctalPulse.lnk");
                if (File.Exists(desktopShortcut))
                {
                    File.Delete(desktopShortcut);
                    InstallerLogger.Info($"Deleted Desktop shortcut: {desktopShortcut}");
                }

                CleanDuplicateShortcuts(Environment.GetFolderPath(Environment.SpecialFolder.Programs), startMenuShortcut);
                CleanDuplicateShortcuts(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), desktopShortcut);
            }
            catch (Exception ex)
            {
                InstallerLogger.Warn($"Failed to remove shortcuts during uninstall: {ex.Message}");
            }

            // 3. Delete Application Installation Folder
            progress?.Report(("Removing application files...", 65));
            try
            {
                if (Directory.Exists(installDir))
                {
                    CleanDirectory(installDir);
                    try { Directory.Delete(installDir, true); } catch { }
                    InstallerLogger.Info($"Removed application directory: {installDir}");
                }
            }
            catch (Exception ex)
            {
                InstallerLogger.Warn($"Failed to completely remove installDir: {ex.Message}");
            }

            // 4. Optionally remove User Data & Settings (%LocalAppData%\OctalPulse)
            if (removeUserData)
            {
                progress?.Report(("Removing user data and settings...", 80));
                try
                {
                    var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OctalPulse");
                    if (Directory.Exists(appData))
                    {
                        var currentExe = Environment.ProcessPath ?? string.Empty;
                        foreach (var file in Directory.GetFiles(appData, "*", SearchOption.AllDirectories))
                        {
                            if (!string.IsNullOrEmpty(currentExe) && string.Equals(Path.GetFullPath(file), Path.GetFullPath(currentExe), StringComparison.OrdinalIgnoreCase))
                                continue;
                            try { File.Delete(file); } catch { }
                        }
                        foreach (var sub in Directory.GetDirectories(appData))
                        {
                            try { Directory.Delete(sub, true); } catch { }
                        }
                        InstallerLogger.Info("Removed user data directory.");
                    }
                }
                catch (Exception ex)
                {
                    InstallerLogger.Warn($"Failed to remove user data directory: {ex.Message}");
                }
            }

            // 5. Remove Registry Entries
            progress?.Report(("Removing registry entries...", 90));
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\OctalPulse", false);
                Registry.CurrentUser.DeleteSubKeyTree(RegistrySubKey, false);
                InstallerLogger.Info("Removed registry entries.");
            }
            catch (Exception ex)
            {
                InstallerLogger.Warn($"Failed to remove registry keys: {ex.Message}");
            }

            progress?.Report(("OctalPulse uninstalled successfully.", 100));
            InstallerLogger.Info("OctalPulse uninstallation completed successfully.");
            return true;
        }
        catch (Exception ex)
        {
            InstallerLogger.Error("Uninstall failed with error", ex);
            throw;
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
