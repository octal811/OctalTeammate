namespace OctalPulse.Application.Services;

public interface ITokenService
{
    string? GetAccessToken();
    string? GetRefreshToken();
    DateTime? GetAccessTokenExpiresAt();
    bool IsAccessTokenExpired();
    void SetTokens(string accessToken, string refreshToken, DateTime accessExpiresAt, DateTime refreshExpiresAt);
    void ClearTokens();
}
