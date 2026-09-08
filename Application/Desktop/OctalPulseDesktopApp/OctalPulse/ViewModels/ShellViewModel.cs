using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Services;
using OctalPulse.Services;

namespace OctalPulse.ViewModels;

public partial class ShellViewModel : ObservableObject, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly IUserSession _userSession;
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;
    private readonly ILocalCacheService _localCache;
    private readonly ISignalRRealtimeService _signalRService;
    private readonly ThemeService _themeService;
    private readonly WpfDialogService _dialogService;
    private DispatcherTimer? _toastTimer;

    public IUserSession UserSession => _userSession;
    public INavigationService Navigation => _navigationService;

    [ObservableProperty]
    private string _currentTheme = "Light";

    [ObservableProperty]
    private string _currentRoute = "Dashboard";

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private bool _isSignalRConnected;

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    // In-app Toast Banner
    [ObservableProperty]
    private string _toastTitle = string.Empty;

    [ObservableProperty]
    private string _toastMessage = string.Empty;

    [ObservableProperty]
    private ToastType _toastType = ToastType.Info;

    [ObservableProperty]
    private bool _isToastVisible;

    public ShellViewModel(
        INavigationService navigationService,
        IUserSession userSession,
        IAuthService authService,
        ITokenService tokenService,
        ILocalCacheService localCache,
        ISignalRRealtimeService signalRService,
        ThemeService themeService,
        IDialogService dialogService)
    {
        _navigationService = navigationService;
        _userSession = userSession;
        _authService = authService;
        _tokenService = tokenService;
        _localCache = localCache;
        _signalRService = signalRService;
        _themeService = themeService;
        _dialogService = (WpfDialogService)dialogService;

        _isSignalRConnected = _signalRService.IsConnected;
        _signalRService.ConnectionStateChanged += OnConnectionStateChanged;
        _dialogService.ToastRequested += OnToastRequested;
    }

    public void OnNavigatedTo(object? parameter)
    {
        if (CurrentView == null)
        {
            NavigateDashboard();
        }
    }

    private void OnConnectionStateChanged(bool connected)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        if (app.Dispatcher.CheckAccess())
        {
            IsSignalRConnected = connected;
        }
        else
        {
            // Never block the UI thread from a SignalR callback. Synchronous
            // Invoke here deadlocks during shutdown, when the UI thread is
            // busy stopping the DI container.
            app.Dispatcher.BeginInvoke(() => IsSignalRConnected = connected);
        }
    }

    private void OnToastRequested(string title, string message, ToastType type)
    {
        void ShowToast()
        {
            ToastTitle = title;
            ToastMessage = message;
            ToastType = type;
            IsToastVisible = true;

            _toastTimer?.Stop();
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _toastTimer.Tick += (s, e) =>
            {
                _toastTimer.Stop();
                IsToastVisible = false;
            };
            _toastTimer.Start();
        }

        var app = System.Windows.Application.Current;
        if (app == null) return;

        if (app.Dispatcher.CheckAccess())
        {
            ShowToast();
        }
        else
        {
            app.Dispatcher.BeginInvoke(ShowToast);
        }
    }

    [RelayCommand]
    private void DismissToast()
    {
        IsToastVisible = false;
        _toastTimer?.Stop();
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
        CurrentTheme = _themeService.CurrentTheme;
    }

    [RelayCommand]
    private void NavigateDashboard()
    {
        CurrentRoute = "Dashboard";
        _navigationService.NavigateTo<DashboardViewModel>();
    }

    [RelayCommand]
    private void NavigateProjects()
    {
        CurrentRoute = "Projects";
        _navigationService.NavigateTo<ProjectsViewModel>();
    }

    [RelayCommand]
    private void NavigateCalendar()
    {
        CurrentRoute = "Calendar";
        _navigationService.NavigateTo<CalendarViewModel>();
    }

    [RelayCommand]
    private void NavigateTasks()
    {
        CurrentRoute = "Tasks";
        _navigationService.NavigateTo<TasksViewModel>();
    }

    [RelayCommand]
    private void NavigateSettings()
    {
        CurrentRoute = "Settings";
        _navigationService.NavigateTo<SettingsViewModel>();
    }

    [RelayCommand]
    private void NavigateStopwatch()
    {
        CurrentRoute = "Stopwatch";
        _navigationService.NavigateTo<StopwatchViewModel>();
    }

    [RelayCommand]
    private void NavigateProfile()
    {
        CurrentRoute = "Profile";
        _navigationService.NavigateTo<ProfileViewModel>();
    }

    [RelayCommand]
    private void NavigateMedia()
    {
        CurrentRoute = "Media";
        _navigationService.NavigateTo<MediaViewModel>();
    }


    [RelayCommand]
    private async Task ReconnectSignalRAsync()
    {
        var token = _tokenService.GetAccessToken();
        if (string.IsNullOrEmpty(token))
        {
            _dialogService.ShowToast("Session Required", "Please log in to reconnect live sync.", ToastType.Warning);
            return;
        }

        _dialogService.ShowToast("Live Sync", "Attempting to reconnect...", ToastType.Info);
        await _signalRService.ConnectAsync(token);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirm = await _dialogService.ShowConfirmationAsync("Sign Out", "Are you sure you want to sign out of OctalPulse?");
        if (!confirm) return;

        CurrentView = null;

        var refreshToken = _tokenService.GetRefreshToken();
        if (!string.IsNullOrEmpty(refreshToken))
        {
            try
            {
                await _authService.RevokeTokenAsync(refreshToken);
            }
            catch
            {
                // Ignore network error on revoke
            }
        }

        await _signalRService.DisconnectAsync();
        _tokenService.ClearTokens();
        _userSession.ClearSession();
        await _localCache.ClearSessionAsync();

        _dialogService.ShowToast("Signed Out", "You have been signed out.", ToastType.Info);
        _navigationService.NavigateTo<LoginViewModel>();
    }
}
