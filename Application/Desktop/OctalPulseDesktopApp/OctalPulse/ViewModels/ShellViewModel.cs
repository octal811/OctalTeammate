using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
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
    private readonly IProjectService _projectService;
    private readonly ThemeService _themeService;
    private readonly WpfDialogService _dialogService;
    private readonly RealtimeNotificationService _notificationService;
    private readonly IAppNotificationService _appNotificationService;
    private DispatcherTimer? _toastTimer;
    private bool _groupsRegistered;

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

    // Notifications Section
    public ObservableCollection<NotificationItemViewModel> Notifications { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnreadNotifications))]
    [NotifyPropertyChangedFor(nameof(UnreadBadgeText))]
    private int _unreadNotificationsCount;

    public bool HasUnreadNotifications => UnreadNotificationsCount > 0;
    public string UnreadBadgeText => UnreadNotificationsCount > 99 ? "99+" : UnreadNotificationsCount.ToString();
    public bool HasNotifications => Notifications.Count > 0;

    [ObservableProperty]
    private bool _isNotificationDrawerOpen;

    [ObservableProperty]
    private bool _isLoadingNotifications;

    [ObservableProperty]
    private bool _isLoadingMoreNotifications;

    [ObservableProperty]
    private bool _hasMoreNotifications;

    [ObservableProperty]
    private int _currentNotificationPage = 1;

    [ObservableProperty]
    private int _totalNotificationsCount;

    public ShellViewModel(
        INavigationService navigationService,
        IUserSession userSession,
        IAuthService authService,
        ITokenService tokenService,
        ILocalCacheService localCache,
        ISignalRRealtimeService signalRService,
        IProjectService projectService,
        ThemeService themeService,
        IDialogService dialogService,
        RealtimeNotificationService notificationService,
        IAppNotificationService appNotificationService)
    {
        _navigationService = navigationService;
        _userSession = userSession;
        _authService = authService;
        _tokenService = tokenService;
        _localCache = localCache;
        _signalRService = signalRService;
        _projectService = projectService;
        _themeService = themeService;
        _dialogService = (WpfDialogService)dialogService;
        _notificationService = notificationService;
        _appNotificationService = appNotificationService;

        _isSignalRConnected = _signalRService.IsConnected;
        _signalRService.ConnectionStateChanged += OnConnectionStateChanged;
        _dialogService.ToastRequested += OnToastRequested;
        _notificationService.NotificationReceived += OnRealtimeNotificationReceived;
        Notifications.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasNotifications));
    }

    public void OnNavigatedTo(object? parameter)
    {
        if (CurrentView == null)
        {
            NavigateDashboard();
        }

        if (!_groupsRegistered)
        {
            _groupsRegistered = true;
            _ = _notificationService.SyncAllUserGroupsAsync();
        }

        _ = LoadInitialNotificationsAsync();
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
            app.Dispatcher.BeginInvoke(() => IsSignalRConnected = connected);
        }

        if (connected)
        {
            _ = _notificationService.SyncAllUserGroupsAsync();
            _ = LoadInitialNotificationsAsync();
        }
    }

    private void OnRealtimeNotificationReceived(NotificationItemDto notification)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        void AddNotification()
        {
            if (notification.Id != Guid.Empty && Notifications.Any(n => n.Id == notification.Id))
            {
                return;
            }

            var itemVm = new NotificationItemViewModel(notification);
            Notifications.Insert(0, itemVm);
            UnreadNotificationsCount++;
            TotalNotificationsCount++;

            var toastTitle = !string.IsNullOrWhiteSpace(notification.Title) ? notification.Title : "Notification";
            var toastMsg = notification.Message;
            if (!string.IsNullOrWhiteSpace(notification.ActorName) &&
                !toastMsg.StartsWith(notification.ActorName, StringComparison.OrdinalIgnoreCase))
            {
                toastMsg = $"{notification.ActorName}: {toastMsg}";
            }

            _dialogService.ShowToast(toastTitle, toastMsg, ToastType.Info);
        }

        if (app.Dispatcher.CheckAccess())
        {
            AddNotification();
        }
        else
        {
            app.Dispatcher.BeginInvoke(AddNotification);
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
    private void ToggleNotificationDrawer()
    {
        IsNotificationDrawerOpen = !IsNotificationDrawerOpen;
        if (IsNotificationDrawerOpen && Notifications.Count == 0)
        {
            _ = LoadInitialNotificationsAsync();
        }
    }

    [RelayCommand]
    private void CloseNotificationDrawer()
    {
        IsNotificationDrawerOpen = false;
    }

    [RelayCommand]
    public async Task LoadInitialNotificationsAsync()
    {
        if (IsLoadingNotifications) return;

        IsLoadingNotifications = true;
        try
        {
            CurrentNotificationPage = 1;
            var result = await _appNotificationService.GetNotificationsAsync(1, 8);

            Notifications.Clear();
            foreach (var item in result.Items)
            {
                Notifications.Add(new NotificationItemViewModel(item));
            }

            TotalNotificationsCount = result.TotalCount;
            UnreadNotificationsCount = result.UnreadCount;
            HasMoreNotifications = result.HasMore;
        }
        finally
        {
            IsLoadingNotifications = false;
        }
    }

    [RelayCommand]
    public async Task LoadMoreNotificationsAsync()
    {
        if (IsLoadingMoreNotifications || !HasMoreNotifications) return;

        IsLoadingMoreNotifications = true;
        try
        {
            var nextPage = CurrentNotificationPage + 1;
            var result = await _appNotificationService.GetNotificationsAsync(nextPage, 8);

            foreach (var item in result.Items)
            {
                Notifications.Add(new NotificationItemViewModel(item));
            }

            CurrentNotificationPage = nextPage;
            TotalNotificationsCount = result.TotalCount;
            HasMoreNotifications = result.HasMore;
        }
        finally
        {
            IsLoadingMoreNotifications = false;
        }
    }

    [RelayCommand]
    public async Task MarkNotificationAsReadAsync(NotificationItemViewModel? item)
    {
        if (item == null || item.IsRead) return;

        item.IsRead = true;
        if (UnreadNotificationsCount > 0)
        {
            UnreadNotificationsCount--;
        }

        await _appNotificationService.MarkAsReadAsync(item.Id);
    }

    [RelayCommand]
    public async Task MarkAllNotificationsAsReadAsync()
    {
        foreach (var item in Notifications)
        {
            item.IsRead = true;
        }

        UnreadNotificationsCount = 0;
        await _appNotificationService.MarkAllAsReadAsync();
    }

    [RelayCommand]
    private async Task ToggleThemeAsync()
    {
        _themeService.ToggleTheme();
        CurrentTheme = _themeService.CurrentTheme;

        var pref = await _localCache.GetPreferencesAsync();
        pref.Theme = CurrentTheme;
        await _localCache.SavePreferencesAsync(pref);
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
    private void NavigateOcto()
    {
        CurrentRoute = "Octo";
        _navigationService.NavigateTo<OctoViewModel>();
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
        _notificationService.NotificationReceived -= OnRealtimeNotificationReceived;
        _groupsRegistered = false;
        _tokenService.ClearTokens();
        _userSession.ClearSession();
        await _localCache.ClearSessionAsync();

        _dialogService.ShowToast("Signed Out", "You have been signed out.", ToastType.Info);
        _navigationService.NavigateTo<LoginViewModel>();
    }
}
