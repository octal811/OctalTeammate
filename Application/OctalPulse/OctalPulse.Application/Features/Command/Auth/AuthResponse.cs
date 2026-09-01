namespace OctalPulse.Application.Features.Command.Auth;

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    Guid UserId,
    string Email,
    string Name);