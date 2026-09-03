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
    private int _step = 1; // 1 = Request code, 2 = Enter code & new password

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
            SuccessMessage = res.Message;
            Step = 2;
            _dialogService.ShowToast("Reset Code Sent", res.Message, ToastType.Info);
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
        if (string.IsNullOrWhiteSpace(Otp) || string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "Please enter the OTP code and your new password.";
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
    private void NavigateToLogin()
    {
        _navigationService.NavigateTo<LoginViewModel>();
    }
}
