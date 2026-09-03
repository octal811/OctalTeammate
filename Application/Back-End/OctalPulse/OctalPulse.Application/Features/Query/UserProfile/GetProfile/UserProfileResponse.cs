using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.UserProfile.GetProfile;

public record UserProfileResponse(
    Guid Id,
    string Name,
    string Email,
    string? PhoneNumber,
    UserRole MainRole,
    UserRank Rank,
    string? ProfilePictureUrl);
