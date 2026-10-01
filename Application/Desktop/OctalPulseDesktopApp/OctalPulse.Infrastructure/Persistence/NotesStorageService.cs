using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Persistence;

public class NotesStorageService : INotesStorageService
{
    private readonly ILogger<NotesStorageService>? _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string PrimaryStoragePath { get; }
    public string EmergencyStoragePath { get; }
    private string SecondaryEmergencyStoragePath { get; }

    public NotesStorageService(ILogger<NotesStorageService>? logger = null)
    {
        _logger = logger;

        // Primary: %LocalAppData%\OctalPulse\Notes\user_notes.json
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        PrimaryStoragePath = Path.Combine(localAppData, "OctalPulse", "Notes", "user_notes.json");

        // Emergency Backup 1: Documents\OctalPulse_EmergencyBackup\Notes\user_notes_emergency.json
        // (Completely isolated from app uninstall or local appdata wipe)
        var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        EmergencyStoragePath = Path.Combine(myDocuments, "OctalPulse_EmergencyBackup", "Notes", "user_notes_emergency.json");

        // Emergency Backup 2: UserProfile\.octal_pulse_emergency_notes\user_notes_emergency.json
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        SecondaryEmergencyStoragePath = Path.Combine(userProfile, ".octal_pulse_emergency_notes", "user_notes_emergency.json");
    }

    public async Task<List<UserNote>> LoadNotesAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // 1. Try Primary
            if (File.Exists(PrimaryStoragePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(PrimaryStoragePath, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var list = JsonSerializer.Deserialize<List<UserNote>>(json, _jsonOptions);
                        if (list != null)
                            return list;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Primary notes file corrupt at {Path}, attempting emergency restore.", PrimaryStoragePath);
                }
            }

            // 2. Try Emergency Folder if primary didn't succeed
            if (File.Exists(EmergencyStoragePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(EmergencyStoragePath, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var list = JsonSerializer.Deserialize<List<UserNote>>(json, _jsonOptions);
                        if (list != null)
                        {
                            _logger?.LogInformation("Successfully restored notes from Emergency Storage: {Path}", EmergencyStoragePath);
                            // Auto-recover primary file
                            await WriteToPathAsync(PrimaryStoragePath, json, cancellationToken);
                            return list;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Could not load from Emergency Storage {Path}", EmergencyStoragePath);
                }
            }

            // 3. Try Secondary Emergency Folder
            if (File.Exists(SecondaryEmergencyStoragePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(SecondaryEmergencyStoragePath, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var list = JsonSerializer.Deserialize<List<UserNote>>(json, _jsonOptions);
                        if (list != null)
                        {
                            await WriteToPathAsync(PrimaryStoragePath, json, cancellationToken);
                            return list;
                        }
                    }
                }
                catch { }
            }

            return new List<UserNote>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveNotesAsync(IEnumerable<UserNote> notes, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = notes.ToList();
            var json = JsonSerializer.Serialize(list, _jsonOptions);

            // 1. Write Primary
            await WriteToPathAsync(PrimaryStoragePath, json, cancellationToken);

            // 2. Write Emergency Copy (in MyDocuments)
            await WriteToPathAsync(EmergencyStoragePath, json, cancellationToken);

            // 3. Write Secondary Copy (in UserProfile)
            await WriteToPathAsync(SecondaryEmergencyStoragePath, json, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<UserNote> AddOrUpdateNoteAsync(UserNote note, CancellationToken cancellationToken = default)
    {
        var existing = await LoadNotesAsync(cancellationToken);
        var idx = existing.FindIndex(n => n.Id == note.Id);
        if (idx >= 0)
        {
            existing[idx] = note;
        }
        else
        {
            existing.Insert(0, note);
        }

        await SaveNotesAsync(existing, cancellationToken);
        return note;
    }

    public async Task DeleteNoteAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var existing = await LoadNotesAsync(cancellationToken);
        var filtered = existing.Where(n => n.Id != noteId).ToList();
        await SaveNotesAsync(filtered, cancellationToken);
    }

    public Task<bool> HasEmergencyBackupAsync(CancellationToken cancellationToken = default)
    {
        bool hasBackup = (File.Exists(EmergencyStoragePath) && new FileInfo(EmergencyStoragePath).Length > 2) ||
                         (File.Exists(SecondaryEmergencyStoragePath) && new FileInfo(SecondaryEmergencyStoragePath).Length > 2);
        return Task.FromResult(hasBackup);
    }

    public async Task<List<UserNote>> RestoreFromEmergencyBackupAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            string? backupPath = null;
            if (File.Exists(EmergencyStoragePath))
                backupPath = EmergencyStoragePath;
            else if (File.Exists(SecondaryEmergencyStoragePath))
                backupPath = SecondaryEmergencyStoragePath;

            if (backupPath == null)
                return new List<UserNote>();

            var json = await File.ReadAllTextAsync(backupPath, cancellationToken);
            var list = JsonSerializer.Deserialize<List<UserNote>>(json, _jsonOptions) ?? new List<UserNote>();

            // Re-write to primary
            await WriteToPathAsync(PrimaryStoragePath, json, cancellationToken);
            return list;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task WriteToPathAsync(string filePath, string content, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, content, ct);
        File.Move(tempPath, filePath, overwrite: true);
    }
}
