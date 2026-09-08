using CommunityToolkit.Mvvm.ComponentModel;
using OctalPulse.Application.Abstractions;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Entities;

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
        // Check for active persisted session in SQLite (the current user)
        var session = await _localCache.GetActiveSessionAsync();
        var accessToken = _tokenService.GetAccessToken();
        var refreshToken = _tokenService.GetRefreshToken();

        // 1) Fast path: fresh access token + remembered session
        if (session != null && session.RememberMe && !string.IsNullOrEmpty(accessToken) && !_tokenService.IsAccessTokenExpired())
        {
            EnterApp(session);
            return;
        }

        // 2) Auto-login by refreshing the stored refresh token
        if (session != null && session.RememberMe && !string.IsNullOrEmpty(refreshToken))
        {
            var refreshExpiry = _tokenService.GetRefreshTokenExpiresAt();
            if (!refreshExpiry.HasValue || DateTime.UtcNow < refreshExpiry.Value)
            {
                try
                {
                    await _authService.RefreshTokenAsync(refreshToken);
                    session.LastLoginAt = DateTime.UtcNow;
                    await _localCache.SaveSessionAsync(session);
                    EnterApp(session);
                    return;
                }
                catch
                {
                    // Refresh token expired/revoked or server unreachable — fall through
                }
            }
        }

        // 3) Refresh unavailable → sign in with the saved email+password (if any)
        if (session != null && session.RememberMe)
        {
            var savedPassword = await _localCache.GetSavedAccountPasswordAsync(session.Email);
            if (!string.IsNullOrEmpty(savedPassword))
            {
                try
                {
                    await _authService.LoginAsync(new LoginRequest(session.Email, savedPassword));
                    session.LastLoginAt = DateTime.UtcNow;
                    await _localCache.SaveSessionAsync(session);
                    EnterApp(session);
                    return;
                }
                catch
                {
                    // Saved credentials rejected — show the login page
                }
            }
        }

        // 4) Fallback: manual login
        _navigationService.NavigateTo<LoginViewModel>();
    }

    private void EnterApp(LocalSession session)
    {
        _userSession.SetSession(session.UserId, session.Email, session.Name, session.MainRole, session.Rank);
        _ = _signalRService.ConnectAsync(_tokenService.GetAccessToken() ?? string.Empty);
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
                        _userSession.SetSession(profile.Id, profile.Email, profile.Name, profile.MainRole, profile.Rank, profile.ProfilePictureUrl);
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
}
