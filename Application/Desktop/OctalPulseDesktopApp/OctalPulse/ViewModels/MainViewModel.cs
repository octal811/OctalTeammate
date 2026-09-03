using CommunityToolkit.Mvvm.ComponentModel;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Services;

namespace OctalPulse.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IUserSession _userSession;
    private readonly ILocalCacheService _localCache;
    private readonly ITokenService _tokenService;
    private readonly ISignalRRealtimeService _signalRService;
    private readonly IAuthService _authService;

    public INavigationService Navigation => _navigationService;

    public MainViewModel(
        INavigationService navigationService,
        IUserSession userSession,
        ILocalCacheService localCache,
        ITokenService tokenService,
        ISignalRRealtimeService signalRService,
        IAuthService authService)
    {
        _navigationService = navigationService;
        _userSession = userSession;
        _localCache = localCache;
        _tokenService = tokenService;
        _signalRService = signalRService;
        _authService = authService;
    }

    public async Task InitializeAsync()
    {
        // Check for active persisted session in SQLite
        var session = await _localCache.GetActiveSessionAsync();
        var accessToken = _tokenService.GetAccessToken();

        if (session != null && session.RememberMe && !string.IsNullOrEmpty(accessToken) && !_tokenService.IsAccessTokenExpired())
        {
            _userSession.SetSession(session.UserId, session.Email, session.Name, session.MainRole, session.Rank);
            _ = _signalRService.ConnectAsync(accessToken);
            _navigationService.NavigateTo<ShellViewModel>();

            // Background sync fresh profile from backend
            _ = Task.Run(async () =>
            {
                try
                {
                    var profile = await _authService.GetProfileAsync();
                    if (profile != null)
                    {
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            _userSession.SetSession(profile.Id, profile.Email, profile.Name, profile.MainRole, profile.Rank);
                        });

                        session.Name = profile.Name;
                        session.MainRole = profile.MainRole;
                        session.Rank = profile.Rank;
                        await _localCache.SaveSessionAsync(session);
                    }
                }
                catch
                {
                    // Server might be offline; preserved SQLite session remains active
                }
            });
        }
        else
        {
            _navigationService.NavigateTo<LoginViewModel>();
        }
    }
}
