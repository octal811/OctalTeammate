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
    }

    public async Task<LocalSession?> GetActiveSessionAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        return await db.Sessions
            .OrderByDescending(s => s.LastLoginAt)
            .FirstOrDefaultAsync(s => s.IsActive)
            .ConfigureAwait(false);
    }

    public async Task SaveSessionAsync(LocalSession session)
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        // Deactivate previous active sessions
        var actives = await db.Sessions.Where(s => s.IsActive).ToListAsync().ConfigureAwait(false);
        foreach (var s in actives)
        {
            s.IsActive = false;
        }

        var existing = await db.Sessions.FirstOrDefaultAsync(s => s.UserId == session.UserId).ConfigureAwait(false);
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
            await db.Sessions.AddAsync(session).ConfigureAwait(false);
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task ClearSessionAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var actives = await db.Sessions.Where(s => s.IsActive).ToListAsync().ConfigureAwait(false);
        foreach (var s in actives)
        {
            s.IsActive = false;
        }
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<UserPreferences> GetPreferencesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync().ConfigureAwait(false);
        var pref = await db.Preferences.FirstOrDefaultAsync(p => p.Id == 1).ConfigureAwait(false);
        if (pref is null)
        {
            pref = new UserPreferences { Id = 1 };
            await db.Preferences.AddAsync(pref).ConfigureAwait(false);
            await db.SaveChangesAsync().ConfigureAwait(false);
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
}
