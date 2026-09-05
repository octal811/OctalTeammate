using System.Globalization;
using OctalPulse.Application.Services;

namespace OctalPulse.Infrastructure.Security;

public class TokenService : ITokenService
{
    private readonly ISecureStorageService _secureStorage;
    private const string AccessTokenKey = "OctalPulse_AccessToken";
    private const string RefreshTokenKey = "OctalPulse_RefreshToken";
    private const string AccessTokenExpiryKey = "OctalPulse_AccessTokenExpiry";
    private const string RefreshTokenExpiryKey = "OctalPulse_RefreshTokenExpiry";

    private string? _cachedAccessToken;
    private string? _cachedRefreshToken;
    private DateTime? _cachedAccessTokenExpiresAt;
    private DateTime? _cachedRefreshTokenExpiresAt;

    public TokenService(ISecureStorageService secureStorage)
    {
        _secureStorage = secureStorage;
        LoadFromStorage();
    }

    private void LoadFromStorage()
    {
        _cachedAccessToken = _secureStorage.GetSecret(AccessTokenKey);
        _cachedRefreshToken = _secureStorage.GetSecret(RefreshTokenKey);
        var expiryStr = _secureStorage.GetSecret(AccessTokenExpiryKey);
        if (DateTime.TryParse(expiryStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiry))
        {
            _cachedAccessTokenExpiresAt = expiry;
        }

        var refreshExpiryStr = _secureStorage.GetSecret(RefreshTokenExpiryKey);
        if (DateTime.TryParse(refreshExpiryStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var refreshExpiry))
        {
            _cachedRefreshTokenExpiresAt = refreshExpiry;
        }
    }

    public string? GetAccessToken() => _cachedAccessToken;

    public string? GetRefreshToken() => _cachedRefreshToken;

    public DateTime? GetAccessTokenExpiresAt() => _cachedAccessTokenExpiresAt;

    public DateTime? GetRefreshTokenExpiresAt() => _cachedRefreshTokenExpiresAt;

    public bool IsAccessTokenExpired()
    {
        if (string.IsNullOrEmpty(_cachedAccessToken))
            return true;

        if (!_cachedAccessTokenExpiresAt.HasValue)
            return false;

        // 30 seconds buffer for clock skew
        return DateTime.UtcNow >= _cachedAccessTokenExpiresAt.Value.AddSeconds(-30);
    }

    public void SetTokens(string accessToken, string refreshToken, DateTime accessExpiresAt, DateTime refreshExpiresAt)
    {
        _cachedAccessToken = accessToken;
        _cachedRefreshToken = refreshToken;
        _cachedAccessTokenExpiresAt = accessExpiresAt;
        _cachedRefreshTokenExpiresAt = refreshExpiresAt;

        _secureStorage.SaveSecret(AccessTokenKey, accessToken);
        _secureStorage.SaveSecret(RefreshTokenKey, refreshToken);
        _secureStorage.SaveSecret(AccessTokenExpiryKey, accessExpiresAt.ToString("O", CultureInfo.InvariantCulture));
        _secureStorage.SaveSecret(RefreshTokenExpiryKey, refreshExpiresAt.ToString("O", CultureInfo.InvariantCulture));
    }

    public void ClearTokens()
    {
        _cachedAccessToken = null;
        _cachedRefreshToken = null;
        _cachedAccessTokenExpiresAt = null;
        _cachedRefreshTokenExpiresAt = null;

        _secureStorage.RemoveSecret(AccessTokenKey);
        _secureStorage.RemoveSecret(RefreshTokenKey);
        _secureStorage.RemoveSecret(AccessTokenExpiryKey);
        _secureStorage.RemoveSecret(RefreshTokenExpiryKey);
    }
}
