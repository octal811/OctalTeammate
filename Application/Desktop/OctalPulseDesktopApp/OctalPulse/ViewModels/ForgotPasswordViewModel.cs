using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Services;

namespace OctalPulse.ViewModels;

public partial class ForgotPasswordViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _otp = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    private int _step = 1; // 1 = Request code, 2 = Enter code & new password

    public bool IsStep1 => Step == 1;
    public bool IsStep2 => Step == 2;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    public ForgotPasswordViewModel(
        IAuthService authService,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task RequestResetCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Please enter your email address.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var res = await _authService.RequestPasswordResetAsync(Email.Trim());
            SuccessMessage = "Please check your email, we sent you an OTP code.";
            Step = 2;
            _dialogService.ShowToast("Reset Code Sent", SuccessMessage, ToastType.Info);
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
    private async Task ConfirmResetPasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(Otp))
        {
            ErrorMessage = "Please enter the 8-digit OTP code sent to your email.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            ErrorMessage = "Please enter and confirm your new password.";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        if (NewPassword.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters long.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var res = await _authService.ResetPasswordAsync(Email.Trim(), Otp.Trim(), NewPassword);
            SuccessMessage = "Password reset successfully! Redirecting to sign in...";
            _dialogService.ShowToast("Password Reset", "Your password was updated successfully. You can now sign in.", ToastType.Success);

            await Task.Delay(1200);
            _navigationService.NavigateTo<LoginViewModel>();
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
    private void BackToStep1()
    {
        Step = 1;
        Otp = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorMessage = null;
        SuccessMessage = null;
    }

    [RelayCommand]
    private void NavigateToLogin()
    {
        Step = 1;
        Otp = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorMessage = null;
        SuccessMessage = null;
        _navigationService.NavigateTo<LoginViewModel>();
    }
}
