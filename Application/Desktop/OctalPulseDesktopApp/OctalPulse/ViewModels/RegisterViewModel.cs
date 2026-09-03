using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.ViewModels;

public partial class RegisterViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private UserRole _selectedRole = UserRole.SoftwareEngineer;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public IReadOnlyList<UserRole> AvailableRoles { get; } = Enum.GetValues<UserRole>();

    public RegisterViewModel(
        IAuthService authService,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _authService = authService;
        _navigationService = navigationService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please fill in all required fields.";
            return;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        if (Password.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters long.";
            return;
        }

        if (!Password.Any(char.IsUpper) || !Password.Any(char.IsLower) || !Password.Any(char.IsDigit) || !Password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            ErrorMessage = "Password must include uppercase, lowercase, a number, and a special character (e.g. !@#$%).";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var request = new RegisterRequest(Email.Trim(), Name.Trim(), SelectedRole, Password);
            await _authService.RegisterAsync(request);

            _dialogService.ShowToast("Registration Successful", "Please verify your email with the OTP sent to your inbox.", ToastType.Success);
            _navigationService.NavigateTo<VerifyEmailViewModel>(new VerifyEmailNavArgs(Email.Trim(), true));
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
