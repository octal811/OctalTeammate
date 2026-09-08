using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.UserProfile.GetProfile;

public record UserProfileResponse(
    Guid Id,
    string Name,
    string Email,
    UserRole MainRole,
    UserRank Rank,
    string? ProfilePictureUrl,
    string? BackgroundImageUrl,
    string? Bio);
