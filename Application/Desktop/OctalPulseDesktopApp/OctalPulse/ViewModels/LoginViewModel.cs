using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Entities;

namespace OctalPulse.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserSession _userSession;
    private readonly ILocalCacheService _localCache;
    private readonly ISignalRRealtimeService _signalRService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;

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

    public LoginViewModel(
        IAuthService authService,
        IUserSession userSession,
        ILocalCacheService localCache,
        ISignalRRealtimeService signalRService,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _authService = authService;
        _userSession = userSession;
        _localCache = localCache;
        _signalRService = signalRService;
        _navigationService = navigationService;
        _dialogService = dialogService;
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

            // Save in SQLite if remember me is active
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
