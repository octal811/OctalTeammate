namespace OctalPulse.Application.Features.Command.Auth.Login;

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    Guid UserId,
    string Email,
    string Name);