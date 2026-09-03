using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class SettingsViewModel : ObservableObject, INavigationAware
{
    private readonly ThemeService _themeService;
    private readonly IUserSession _userSession;
    private readonly ILocalCacheService _localCache;
    private readonly ISecureStorageService _secureStorage;
    private readonly IGitHubService _gitHubService;
    private readonly IAuthService _authService;
    private readonly IDialogService _dialogService;

    private const string GitHubTokenSecretKey = "OctalPulse_GitHubToken";

    public IUserSession UserSession => _userSession;

    [ObservableProperty]
    private string _currentTheme = "Light";

    // User Profile Management
    [ObservableProperty]
    private string _profileName = string.Empty;

    [ObservableProperty]
    private UserRole _profileRole = UserRole.SoftwareEngineer;

    [ObservableProperty]
    private bool _isSavingProfile;

    public ObservableCollection<UserRole> AvailableRoles { get; } = new(Enum.GetValues<UserRole>());

    // GitHub Integration
    [ObservableProperty]
    private string _gitHubTokenInput = string.Empty;

    [ObservableProperty]
    private bool _isGitHubConnected;

    [ObservableProperty]
    private GitHubUserInfo? _gitHubUser;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<GitHubRepoInfo> Repositories { get; } = new();

    public SettingsViewModel(
        ThemeService themeService,
        IUserSession userSession,
        ILocalCacheService localCache,
        ISecureStorageService secureStorage,
        IGitHubService gitHubService,
        IAuthService authService,
        IDialogService dialogService)
    {
        _themeService = themeService;
        _userSession = userSession;
        _localCache = localCache;
        _secureStorage = secureStorage;
        _gitHubService = gitHubService;
        _authService = authService;
        _dialogService = dialogService;

        _currentTheme = _themeService.CurrentTheme;
    }

    public void OnNavigatedTo(object? parameter)
    {
        _ = LoadSettingsAsync();
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        IsBusy = true;
        try
        {
            // Populate profile from current session
            ProfileName = _userSession.Name ?? string.Empty;
            ProfileRole = _userSession.MainRole ?? UserRole.SoftwareEngineer;

            // Attempt to load fresh profile details from backend
            try
            {
                var me = await _authService.GetProfileAsync();
                if (me != null)
                {
                    ProfileName = me.Name;
                    ProfileRole = me.MainRole;
                    _userSession.SetSession(me.Id, me.Email, me.Name, me.MainRole, me.Rank);
                }
            }
            catch
            {
                // Offline or server not ready; local session values preserved
            }

            var pref = await _localCache.GetPreferencesAsync();
            CurrentTheme = pref.Theme;

            // Load GitHub state
            var ghSettings = await _localCache.GetGitHubSettingsAsync();
            IsGitHubConnected = ghSettings.IsConnected;

            var storedToken = _secureStorage.GetSecret(GitHubTokenSecretKey);
            if (IsGitHubConnected && !string.IsNullOrEmpty(storedToken))
            {
                GitHubUser = await _gitHubService.GetUserProfileAsync(storedToken);
                var repos = await _gitHubService.GetUserRepositoriesAsync(storedToken);
                Repositories.Clear();
                foreach (var r in repos)
                {
                    Repositories.Add(r);
                }
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Settings Load Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            _dialogService.ShowToast("Validation Error", "Name cannot be empty.", ToastType.Warning);
            return;
        }

        IsSavingProfile = true;
        try
        {
            var updated = await _authService.UpdateProfileAsync(new UpdateUserProfileRequest(
                ProfileName.Trim(),
                ProfileRole,
                null));

            _userSession.SetSession(updated.Id, updated.Email, updated.Name, updated.MainRole, updated.Rank);

            // Update SQLite cached session as well
            var session = await _localCache.GetActiveSessionAsync();
            if (session != null)
            {
                session.Name = updated.Name;
                session.MainRole = updated.MainRole;
                session.Rank = updated.Rank;
                await _localCache.SaveSessionAsync(session);
            }

            _dialogService.ShowToast("Profile Updated", "Your profile details have been saved.", ToastType.Success);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Profile Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsSavingProfile = false;
        }
    }

    [RelayCommand]
    private async Task SelectThemeAsync(string theme)
    {
        CurrentTheme = theme;
        _themeService.SetTheme(theme);

        var pref = await _localCache.GetPreferencesAsync();
        pref.Theme = theme;
        await _localCache.SavePreferencesAsync(pref);

        _dialogService.ShowToast("Theme Changed", $"Active theme switched to {theme}.", ToastType.Info);
    }

    [RelayCommand]
    private async Task ConnectGitHubAsync()
    {
        if (string.IsNullOrWhiteSpace(GitHubTokenInput))
        {
            _dialogService.ShowToast("Validation Error", "Please provide a GitHub Personal Access Token.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var isValid = await _gitHubService.ValidateTokenAsync(GitHubTokenInput.Trim());
            if (!isValid)
            {
                _dialogService.ShowToast("GitHub Error", "The token could not be verified with GitHub.", ToastType.Error);
                return;
            }

            // Securely store with Windows DPAPI
            _secureStorage.SaveSecret(GitHubTokenSecretKey, GitHubTokenInput.Trim());

            var profile = await _gitHubService.GetUserProfileAsync(GitHubTokenInput.Trim());
            GitHubUser = profile;

            var ghSettings = await _localCache.GetGitHubSettingsAsync();
            ghSettings.IsConnected = true;
            ghSettings.GitHubUsername = profile?.Login;
            ghSettings.AvatarUrl = profile?.AvatarUrl;
            ghSettings.ConnectedAt = DateTime.UtcNow;
            await _localCache.SaveGitHubSettingsAsync(ghSettings);

            IsGitHubConnected = true;
            GitHubTokenInput = string.Empty;

            var repos = await _gitHubService.GetUserRepositoriesAsync(_secureStorage.GetSecret(GitHubTokenSecretKey)!);
            Repositories.Clear();
            foreach (var r in repos)
            {
                Repositories.Add(r);
            }

            _dialogService.ShowToast("GitHub Connected", $"Successfully connected as @{profile?.Login}!", ToastType.Success);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("GitHub Connection Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectGitHubAsync()
    {
        var confirm = await _dialogService.ShowConfirmationAsync("Disconnect GitHub", "Disconnect your GitHub account and delete stored credentials?", "Disconnect", "Cancel");
        if (!confirm) return;

        _secureStorage.RemoveSecret(GitHubTokenSecretKey);

        var ghSettings = await _localCache.GetGitHubSettingsAsync();
        ghSettings.IsConnected = false;
        ghSettings.GitHubUsername = null;
        ghSettings.AvatarUrl = null;
        ghSettings.ConnectedAt = null;
        await _localCache.SaveGitHubSettingsAsync(ghSettings);

        IsGitHubConnected = false;
        GitHubUser = null;
        Repositories.Clear();

        _dialogService.ShowToast("GitHub Disconnected", "GitHub integration removed.", ToastType.Info);
    }
}
