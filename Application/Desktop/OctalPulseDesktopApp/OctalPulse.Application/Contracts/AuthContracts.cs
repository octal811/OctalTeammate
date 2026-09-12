using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Contracts;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    Guid UserId,
    string Email,
    string Name,
    UserRole MainRole,
    UserRank Rank);

public record UserProfileResponse(
    Guid Id,
    string Name,
    string Email,
    UserRole MainRole,
    UserRank Rank,
    string? ProfilePictureUrl,
    string? BackgroundImageUrl,
    string? Bio);

public record UserSearchResult(
    Guid Id,
    string Name,
    string Email,
    UserRole MainRole,
    UserRank Rank,
    string? ProfilePictureUrl);

public record SearchUsersResponse(IReadOnlyList<UserSearchResult> Results);

public record UpdateUserProfileRequest(
    string Name,
    UserRole MainRole,
    string? Bio,
    string? ProfilePictureUrl);

public record RegisterRequest(
    string Email,
    string Name,
    UserRole MainRole,
    string Password);

public record RegisterResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    Guid UserId,
    string Email,
    string Name);

public record RefreshTokenRequest(string RefreshToken);

public record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    Guid UserId,
    string Email,
    string Name);

public record RevokeTokenRequest(string RefreshToken);

public record RequestEmailVerificationRequest(string Email);

public record VerifyEmailRequest(string Email, string Otp);

public record RequestPasswordResetRequest(string Email);

public record ResetPasswordRequest(string Email, string Otp, string NewPassword);

public record MessageResponse(string Message);
