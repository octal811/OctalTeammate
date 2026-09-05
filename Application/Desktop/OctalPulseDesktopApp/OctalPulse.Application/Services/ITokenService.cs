namespace OctalPulse.Application.Services;

public interface ITokenService
{
    string? GetAccessToken();
    string? GetRefreshToken();
    DateTime? GetAccessTokenExpiresAt();
    DateTime? GetRefreshTokenExpiresAt();
    bool IsAccessTokenExpired();
    void SetTokens(string accessToken, string refreshToken, DateTime accessExpiresAt, DateTime refreshExpiresAt);
    void ClearTokens();
}
