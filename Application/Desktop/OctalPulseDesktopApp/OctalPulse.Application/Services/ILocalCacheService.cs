using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Services;

public interface ILocalCacheService
{
    // Session & Preferences
    Task<LocalSession?> GetActiveSessionAsync();
    Task SaveSessionAsync(LocalSession session);
    Task ClearSessionAsync();
    Task<UserPreferences> GetPreferencesAsync();
    Task SavePreferencesAsync(UserPreferences preferences);

    // GitHub Settings
    Task<GitHubIntegrationSettings> GetGitHubSettingsAsync();
    Task SaveGitHubSettingsAsync(GitHubIntegrationSettings settings);

    // Saved accounts (remember-me credentials)
    Task<IReadOnlyList<SavedAccount>> GetSavedAccountsAsync();
    Task<string?> GetSavedAccountPasswordAsync(string email);
    Task SaveSavedAccountAsync(string email, string password);
    Task RemoveSavedAccountAsync(string email);
}
