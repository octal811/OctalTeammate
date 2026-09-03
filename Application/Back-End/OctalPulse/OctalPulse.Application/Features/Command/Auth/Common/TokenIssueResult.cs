namespace OctalPulse.Application.Features.Command.Auth.Common;

public record TokenIssueResult(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    Guid UserId,
    string Email,
    string Name);