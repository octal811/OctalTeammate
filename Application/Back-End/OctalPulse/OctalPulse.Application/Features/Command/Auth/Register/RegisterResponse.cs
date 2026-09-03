namespace OctalPulse.Application.Features.Command.Auth.Register;

public record RegisterResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    Guid UserId,
    string Email,
    string Name);