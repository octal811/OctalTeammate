using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Persistence;

public class LocalCacheService : ILocalCacheService
{
    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private static readonly byte[] CredentialEntropy = Encoding.UTF8.GetBytes("OctalPulse.SavedCredentials.2026");

    public LocalCacheService(IDbContextFactory<LocalAppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var db = _dbFactory.CreateDbContext();
        db.Database.EnsureCreated();

        // EnsureCreated does not add new tables to an existing database.
        // Create the SavedAccounts table explicitly so remember-me credentials
        // work on databases that were created before this feature shipped.
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "SavedAccounts" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_SavedAccounts" PRIMARY KEY,
                "Email" TEXT NOT NULL,
                "EncryptedPassword" TEXT NOT NULL,
                "LastUsedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_SavedAccounts_Email" ON "SavedAccounts" ("Email");
            """);

        // EnsureCreated does not add new columns to an existing database. Add the
        // WorkTimeSeconds column to MinorTasks manually (SQLite throws if it exists).
        try
        {
            db.Database.ExecuteSqlRaw("""
                ALTER TABLE "MinorTasks" ADD COLUMN "WorkTimeSeconds" INTEGER NULL;
                """);
        }
        catch (Exception)
        {
            // Column already exists on databases created after this feature shipped.
        }
    }

    public async Task<LocalSession?> GetActiveSessionAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Sessions
            .OrderByDescending(s => s.LastLoginAt)
            .FirstOrDefaultAsync(s => s.IsActive);
    }

    public async Task SaveSessionAsync(LocalSession session)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        // Deactivate previous active sessions
        var actives = await db.Sessions.Where(s => s.IsActive).ToListAsync();
        foreach (var s in actives)
        {
            s.IsActive = false;
        }

        var existing = await db.Sessions.FirstOrDefaultAsync(s => s.UserId == session.UserId);
        if (existing is not null)
        {
            existing.Email = session.Email;
            existing.Name = session.Name;
            existing.MainRole = session.MainRole;
            existing.Rank = session.Rank;
            existing.RememberMe = session.RememberMe;
            existing.LastLoginAt = DateTime.UtcNow;
            existing.IsActive = true;
        }
        else
        {
            session.IsActive = true;
            session.LastLoginAt = DateTime.UtcNow;
            await db.Sessions.AddAsync(session);
        }

        await db.SaveChangesAsync();
    }

    public async Task ClearSessionAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var actives = await db.Sessions.Where(s => s.IsActive).ToListAsync();
        foreach (var s in actives)
        {
            s.IsActive = false;
        }
        await db.SaveChangesAsync();
    }

    public async Task<UserPreferences> GetPreferencesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var pref = await db.Preferences.FirstOrDefaultAsync(p => p.Id == 1);
        if (pref is null)
        {
            pref = new UserPreferences { Id = 1 };
            await db.Preferences.AddAsync(pref);
            await db.SaveChangesAsync();
        }
        return pref;
    }

    public async Task SavePreferencesAsync(UserPreferences preferences)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.Preferences.FirstOrDefaultAsync(p => p.Id == 1);
        if (existing is not null)
        {
            existing.Theme = preferences.Theme;
            existing.AccentColor = preferences.AccentColor;
            existing.IsSidebarCollapsed = preferences.IsSidebarCollapsed;
            existing.NotificationsEnabled = preferences.NotificationsEnabled;
            existing.AutoReconnectSignalR = preferences.AutoReconnectSignalR;
        }
        else
        {
            preferences.Id = 1;
            await db.Preferences.AddAsync(preferences);
        }
        await db.SaveChangesAsync();
    }

    public async Task<GitHubIntegrationSettings> GetGitHubSettingsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var settings = await db.GitHubSettings.FirstOrDefaultAsync(g => g.Id == 1);
        if (settings is null)
        {
            settings = new GitHubIntegrationSettings { Id = 1, IsConnected = false };
            await db.GitHubSettings.AddAsync(settings);
            await db.SaveChangesAsync();
        }
        return settings;
    }

    public async Task SaveGitHubSettingsAsync(GitHubIntegrationSettings settings)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.GitHubSettings.FirstOrDefaultAsync(g => g.Id == 1);
        if (existing is not null)
        {
            existing.IsConnected = settings.IsConnected;
            existing.GitHubUsername = settings.GitHubUsername;
            existing.AvatarUrl = settings.AvatarUrl;
            existing.DefaultRepository = settings.DefaultRepository;
            existing.ConnectedAt = settings.ConnectedAt;
        }
        else
        {
            settings.Id = 1;
            await db.GitHubSettings.AddAsync(settings);
        }
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<SavedAccount>> GetSavedAccountsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.SavedAccounts.OrderByDescending(a => a.LastUsedAt).ToListAsync();
    }

    public async Task<string?> GetSavedAccountPasswordAsync(string email)
    {
        if (!OperatingSystem.IsWindows()) return null;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var account = await db.SavedAccounts.FirstOrDefaultAsync(a => a.Email.ToLower() == email.Trim().ToLower());
        return account is null ? null : DecryptPassword(account.EncryptedPassword);
    }

    public async Task SaveSavedAccountAsync(string email, string password)
    {
        var normalized = email.Trim();
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.SavedAccounts.FirstOrDefaultAsync(a => a.Email.ToLower() == normalized.ToLower());
        if (existing is not null)
        {
            existing.EncryptedPassword = OperatingSystem.IsWindows() ? EncryptPassword(password) : string.Empty;
            existing.LastUsedAt = DateTime.UtcNow;
        }
        else
        {
            await db.SavedAccounts.AddAsync(new SavedAccount
            {
                Email = normalized,
                EncryptedPassword = OperatingSystem.IsWindows() ? EncryptPassword(password) : string.Empty,
                LastUsedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    public async Task RemoveSavedAccountAsync(string email)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.SavedAccounts.FirstOrDefaultAsync(a => a.Email.ToLower() == email.Trim().ToLower());
        if (existing is not null)
        {
            db.SavedAccounts.Remove(existing);
            await db.SaveChangesAsync();
        }
    }

    [SupportedOSPlatform("windows")]
    private static string EncryptPassword(string password)
    {
        var plain = Encoding.UTF8.GetBytes(password);
        var encrypted = ProtectedData.Protect(plain, CredentialEntropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    [SupportedOSPlatform("windows")]
    private static string? DecryptPassword(string encrypted)
    {
        try
        {
            var data = Convert.FromBase64String(encrypted);
            var plain = ProtectedData.Unprotect(data, CredentialEntropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<CachedProject>> GetCachedProjectsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Projects.OrderByDescending(p => p.CreatedDate).ToListAsync();
    }

    public async Task SaveProjectsAsync(IEnumerable<CachedProject> projects)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        foreach (var p in projects)
        {
            var existing = await db.Projects.FindAsync(p.Id);
            if (existing is not null)
            {
                existing.Title = p.Title;
                existing.Description = p.Description;
                existing.Progress = p.Progress;
                existing.Status = p.Status;
                existing.ModifiedDate = p.ModifiedDate;
                existing.MembersCount = p.MembersCount;
                existing.LastSyncedAt = DateTime.UtcNow;
            }
            else
            {
                p.LastSyncedAt = DateTime.UtcNow;
                await db.Projects.AddAsync(p);
            }
        }
        await db.SaveChangesAsync();
    }

    public async Task<CachedProject?> GetCachedProjectByIdAsync(Guid projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Projects.FindAsync(projectId);
    }

    public async Task<IReadOnlyList<CachedTrack>> GetCachedTracksAsync(Guid projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Tracks.Where(t => t.ProjectId == projectId).ToListAsync();
    }

    public async Task SaveTracksAsync(Guid projectId, IEnumerable<CachedTrack> tracks)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existingTracks = await db.Tracks.Where(t => t.ProjectId == projectId).ToListAsync();
        db.Tracks.RemoveRange(existingTracks);

        foreach (var t in tracks)
        {
            t.ProjectId = projectId;
            t.LastSyncedAt = DateTime.UtcNow;
            await db.Tracks.AddAsync(t);
        }
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<CachedMajorTask>> GetCachedMajorTasksAsync(Guid trackId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.MajorTasks.Where(t => t.TrackId == trackId).OrderBy(t => t.Order).ToListAsync();
    }

    public async Task SaveMajorTasksAsync(Guid trackId, IEnumerable<CachedMajorTask> tasks)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existingTasks = await db.MajorTasks.Where(t => t.TrackId == trackId).ToListAsync();
        db.MajorTasks.RemoveRange(existingTasks);

        foreach (var t in tasks)
        {
            t.TrackId = trackId;
            t.LastSyncedAt = DateTime.UtcNow;
            await db.MajorTasks.AddAsync(t);
        }
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<CachedMinorTask>> GetCachedMinorTasksAsync(Guid majorTaskId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.MinorTasks.Where(t => t.MajorTaskId == majorTaskId).OrderBy(t => t.Order).ToListAsync();
    }

    public async Task SaveMinorTasksAsync(Guid majorTaskId, IEnumerable<CachedMinorTask> tasks)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existingMinor = await db.MinorTasks.Where(t => t.MajorTaskId == majorTaskId).ToListAsync();
        db.MinorTasks.RemoveRange(existingMinor);

        foreach (var t in tasks)
        {
            t.MajorTaskId = majorTaskId;
            t.LastSyncedAt = DateTime.UtcNow;
            await db.MinorTasks.AddAsync(t);
        }
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<CachedEvent>> GetCachedEventsAsync(Guid projectId, int year, int month)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Events
            .Where(e => e.ProjectId == projectId && e.StartDate.Year == year && e.StartDate.Month == month)
            .OrderBy(e => e.StartDate)
            .ToListAsync();
    }

    public async Task SaveEventsAsync(Guid projectId, int year, int month, IEnumerable<CachedEvent> events)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existingEvents = await db.Events
            .Where(e => e.ProjectId == projectId && e.StartDate.Year == year && e.StartDate.Month == month)
            .ToListAsync();
        db.Events.RemoveRange(existingEvents);

        foreach (var e in events)
        {
            e.ProjectId = projectId;
            e.LastSyncedAt = DateTime.UtcNow;
            await db.Events.AddAsync(e);
        }
        await db.SaveChangesAsync();
    }
}
