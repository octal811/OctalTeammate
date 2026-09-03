using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public record VerifyEmailNavArgs(string Email, bool OtpAlreadySent);

public partial class VerifyEmailViewModel : ObservableObject, INavigationAware
{
    private readonly IAuthService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private DispatcherTimer? _countdownTimer;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _otp = string.Empty;

    [ObservableProperty]
    private int _remainingSeconds = 0;

    [ObservableProperty]
    private string _countdownDisplay = "Not sent yet";

    [ObservableProperty]
    private string _resendButtonText = "Send Code";

    [ObservableProperty]
    private bool _canResend = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    public VerifyEmailViewModel(
        IAuthService authService,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
    }

    public void OnNavigatedTo(object? parameter)
    {
        if (parameter is VerifyEmailNavArgs args)
        {
            Email = args.Email;
            if (args.OtpAlreadySent)
            {
                StartTimer();
            }
            else
            {
                ResetToInitialState();
            }
        }
        else if (parameter is string email && !string.IsNullOrWhiteSpace(email))
        {
            Email = email;
            ResetToInitialState();
        }
        else
        {
            ResetToInitialState();
        }
    }

    private void ResetToInitialState()
    {
        _countdownTimer?.Stop();
        RemainingSeconds = 0;
        CountdownDisplay = "Not sent yet";
        CanResend = true;
        ResendButtonText = "Send Code";
    }

    private void StartTimer()
    {
        _countdownTimer?.Stop();
        RemainingSeconds = 180; // 3 minutes
        CanResend = false;
        ResendButtonText = "Resend Code";
        UpdateDisplay();

        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _countdownTimer.Tick += (s, e) =>
        {
            if (RemainingSeconds > 0)
            {
                RemainingSeconds--;
                UpdateDisplay();
            }
            else
            {
                _countdownTimer?.Stop();
                CountdownDisplay = "Code expired";
                CanResend = true; // Enabled when expired after 3 minutes
                ResendButtonText = "Resend Code";
            }
        };
        _countdownTimer.Start();
    }

    private void UpdateDisplay()
    {
        var minutes = RemainingSeconds / 60;
        var seconds = RemainingSeconds % 60;
        CountdownDisplay = $"{minutes:D2}:{seconds:D2}";
    }

    [RelayCommand]
    private async Task VerifyOtpAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Otp))
        {
            ErrorMessage = "Please enter your email and the 8-digit OTP code.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var response = await _authService.VerifyEmailAsync(Email.Trim(), Otp.Trim());
            _countdownTimer?.Stop();
            SuccessMessage = response.Message;
            _dialogService.ShowToast("Verified", "Your email has been verified successfully! You may now sign in.", ToastType.Success);

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
    private async Task ResendOtpAsync()
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
            var response = await _authService.RequestEmailVerificationAsync(Email.Trim());
            _dialogService.ShowToast("Code Sent", response.Message, ToastType.Info);
            StartTimer();
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
        _countdownTimer?.Stop();
        _navigationService.NavigateTo<LoginViewModel>();
    }
}
