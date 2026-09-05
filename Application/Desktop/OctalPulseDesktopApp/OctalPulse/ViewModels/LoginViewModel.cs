using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class LoginViewModel : ObservableObject, INavigationAware
{
    private readonly IAuthService _authService;
    private readonly IUserSession _userSession;
    private readonly ILocalCacheService _localCache;
    private readonly ISignalRRealtimeService _signalRService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ITokenService _tokenService;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _rememberMe = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isPasswordVisible;

    public ObservableCollection<SavedAccount> SavedAccounts { get; } = new();

    public LoginViewModel(
        IAuthService authService,
        IUserSession userSession,
        ILocalCacheService localCache,
        ISignalRRealtimeService signalRService,
        INavigationService navigationService,
        IDialogService dialogService,
        ITokenService tokenService)
    {
        _authService = authService;
        _userSession = userSession;
        _localCache = localCache;
        _signalRService = signalRService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _tokenService = tokenService;
    }

    public void OnNavigatedTo(object? parameter)
    {
        _ = LoadSavedAccountsAsync();
    }

    public bool HasSavedAccounts => SavedAccounts.Count > 0;

    private async Task LoadSavedAccountsAsync()
    {
        var accounts = await _localCache.GetSavedAccountsAsync();
        SavedAccounts.Clear();
        foreach (var account in accounts)
        {
            SavedAccounts.Add(account);
        }
        OnPropertyChanged(nameof(HasSavedAccounts));
    }

    [RelayCommand]
    private async Task SignInWithSavedAsync(SavedAccount account)
    {
        if (account is null) return;

        var savedPassword = await _localCache.GetSavedAccountPasswordAsync(account.Email);
        if (string.IsNullOrEmpty(savedPassword))
        {
            ErrorMessage = "Could not decrypt the saved password for this account.";
            return;
        }

        Email = account.Email;
        Password = savedPassword;
        ErrorMessage = null;
        _dialogService.ShowToast("Account selected", $"Password filled for {account.Email}.", ToastType.Info);
    }

    [RelayCommand]
    private async Task RemoveSavedAsync(SavedAccount account)
    {
        if (account is null) return;

        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Remove saved account",
            $"Remove \"{account.Email}\" from this device?\nThis deletes the saved password and remembered session.");

        if (!confirmed) return;

        await _localCache.RemoveSavedAccountAsync(account.Email);

        // If this was the remembered session, deactivate it and purge any stored tokens
        var active = await _localCache.GetActiveSessionAsync();
        if (active != null && string.Equals(active.Email, account.Email, StringComparison.OrdinalIgnoreCase))
        {
            await _localCache.ClearSessionAsync();
            var storedRefreshToken = _tokenService.GetRefreshToken();
            if (!string.IsNullOrEmpty(storedRefreshToken))
            {
                try { await _authService.RevokeTokenAsync(storedRefreshToken); }
                catch { /* token already expired or server offline */ }
            }
            _tokenService.ClearTokens();
        }

        SavedAccounts.Remove(account);
        OnPropertyChanged(nameof(HasSavedAccounts));

        if (string.Equals(Email, account.Email, StringComparison.OrdinalIgnoreCase))
        {
            Email = string.Empty;
            Password = string.Empty;
        }

        _dialogService.ShowToast("Account removed", $"{account.Email} was removed from this device.", ToastType.Info);
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your email and password.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var response = await _authService.LoginAsync(new LoginRequest(Email.Trim(), Password));

            // Populate active session
            _userSession.SetSession(
                response.UserId,
                response.Email,
                response.Name,
                response.MainRole,
                response.Rank);

            if (RememberMe)
            {
                await _localCache.SaveSessionAsync(new LocalSession
                {
                    UserId = response.UserId,
                    Email = response.Email,
                    Name = response.Name,
                    MainRole = response.MainRole,
                    Rank = response.Rank,
                    RememberMe = true,
                    LastLoginAt = DateTime.UtcNow
                });

                // Persist encrypted credentials for auto-login when the refresh token expires
                await _localCache.SaveSavedAccountAsync(response.Email, Password);
            }
            else
            {
                // Do NOT persist an auto-login session or saved credentials
                await _localCache.ClearSessionAsync();
                await _localCache.RemoveSavedAccountAsync(response.Email);
            }

            // Connect real-time hub
            _ = _signalRService.ConnectAsync(response.AccessToken);

            _dialogService.ShowToast("Welcome back!", $"Signed in as {response.Name}", ToastType.Success);
            _navigationService.NavigateTo<ShellViewModel>();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NavigateToRegister()
    {
        _navigationService.NavigateTo<RegisterViewModel>();
    }

    [RelayCommand]
    private void NavigateToForgotPassword()
    {
        _navigationService.NavigateTo<ForgotPasswordViewModel>();
    }

    [RelayCommand]
    private void NavigateToVerifyEmail()
    {
        _navigationService.NavigateTo<VerifyEmailViewModel>(new VerifyEmailNavArgs(Email, false));
    }
}
