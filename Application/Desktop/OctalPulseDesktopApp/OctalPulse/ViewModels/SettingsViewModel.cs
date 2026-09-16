using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class SettingsViewModel : ObservableObject, INavigationAware
{
    private readonly ThemeService _themeService;
    private readonly ILocalCacheService _localCache;
    private readonly ISecureStorageService _secureStorage;
    private readonly IGitHubService _gitHubService;
    private readonly IGeminiClient _geminiClient;
    private readonly IDialogService _dialogService;

    private const string GitHubTokenSecretKey = "OctalPulse_GitHubToken";
    private const string GeminiApiKeySecretKey = "OctalPulse_GeminiApiKey";

    [ObservableProperty]
    private string _currentTheme = "Light";

    // GitHub Integration
    [ObservableProperty]
    private string _gitHubTokenInput = string.Empty;

    [ObservableProperty]
    private bool _isGitHubConnected;

    [ObservableProperty]
    private GitHubUserInfo? _gitHubUser;

    // Google Gemini Integration (Octo AI Supporter)
    [ObservableProperty]
    private string _geminiApiKeyInput = string.Empty;

    [ObservableProperty]
    private bool _isGeminiConnected;

    [ObservableProperty]
    private string _geminiModel = "gemini-1.5-flash";

    [ObservableProperty]
    private string _geminiMaskedKeyDisplay = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<GitHubRepoInfo> Repositories { get; } = new();

    public SettingsViewModel(
        ThemeService themeService,
        ILocalCacheService localCache,
        ISecureStorageService secureStorage,
        IGitHubService gitHubService,
        IGeminiClient geminiClient,
        IDialogService dialogService)
    {
        _themeService = themeService;
        _localCache = localCache;
        _secureStorage = secureStorage;
        _gitHubService = gitHubService;
        _geminiClient = geminiClient;
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

            // Load Gemini state
            var storedGeminiKey = _secureStorage.GetSecret(GeminiApiKeySecretKey);
            IsGeminiConnected = !string.IsNullOrWhiteSpace(storedGeminiKey);
            if (IsGeminiConnected && storedGeminiKey!.Length > 8)
            {
                GeminiMaskedKeyDisplay = $"{storedGeminiKey[..4]}...{storedGeminiKey[^4..]}";
                var bestModel = await _geminiClient.ResolveBestModelAsync(storedGeminiKey);
                if (!string.IsNullOrEmpty(bestModel))
                {
                    GeminiModel = bestModel;
                }
            }
            else
            {
                GeminiMaskedKeyDisplay = IsGeminiConnected ? "••••••••" : string.Empty;
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

    [RelayCommand]
    private async Task ConnectGeminiAsync()
    {
        if (string.IsNullOrWhiteSpace(GeminiApiKeyInput))
        {
            _dialogService.ShowToast("Validation Error", "Please provide a Google Gemini API key.", ToastType.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var isValid = await _geminiClient.ValidateApiKeyAsync(GeminiApiKeyInput.Trim(), GeminiModel);
            if (!isValid)
            {
                _dialogService.ShowToast("Gemini Validation Error", "The key could not be verified with Google Gemini. Please check the key and your network connection.", ToastType.Error);
                return;
            }

            // Securely store with Windows DPAPI
            _secureStorage.SaveSecret(GeminiApiKeySecretKey, GeminiApiKeyInput.Trim());

            var rawKey = GeminiApiKeyInput.Trim();
            GeminiMaskedKeyDisplay = rawKey.Length > 8 ? $"{rawKey[..4]}...{rawKey[^4..]}" : "••••••••";
            IsGeminiConnected = true;
            GeminiApiKeyInput = string.Empty;

            _dialogService.ShowToast("Gemini Connected", "Google Gemini connected successfully! Octo AI Supporter is ready to assist.", ToastType.Success);
        }
        catch (Exception ex)
        {
            _dialogService.ShowToast("Gemini Connection Error", ex.Message, ToastType.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectGeminiAsync()
    {
        var confirm = await _dialogService.ShowConfirmationAsync("Disconnect Gemini", "Disconnect Google Gemini and delete the securely stored API key? Octo AI Supporter will be disabled.", "Disconnect", "Cancel");
        if (!confirm) return;

        _secureStorage.RemoveSecret(GeminiApiKeySecretKey);

        IsGeminiConnected = false;
        GeminiMaskedKeyDisplay = string.Empty;

        _dialogService.ShowToast("Gemini Disconnected", "Google Gemini API key removed.", ToastType.Info);
    }
}
