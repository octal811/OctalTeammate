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

    // Cached Entities for offline browsing
    Task<IReadOnlyList<CachedProject>> GetCachedProjectsAsync();
    Task SaveProjectsAsync(IEnumerable<CachedProject> projects);
    Task<CachedProject?> GetCachedProjectByIdAsync(Guid projectId);
    Task<IReadOnlyList<CachedTrack>> GetCachedTracksAsync(Guid projectId);
    Task SaveTracksAsync(Guid projectId, IEnumerable<CachedTrack> tracks);
    Task<IReadOnlyList<CachedMajorTask>> GetCachedMajorTasksAsync(Guid trackId);
    Task SaveMajorTasksAsync(Guid trackId, IEnumerable<CachedMajorTask> tasks);
    Task<IReadOnlyList<CachedMinorTask>> GetCachedMinorTasksAsync(Guid majorTaskId);
    Task SaveMinorTasksAsync(Guid majorTaskId, IEnumerable<CachedMinorTask> tasks);
    Task<IReadOnlyList<CachedEvent>> GetCachedEventsAsync(Guid projectId, int year, int month);
    Task SaveEventsAsync(Guid projectId, int year, int month, IEnumerable<CachedEvent> events);
}
