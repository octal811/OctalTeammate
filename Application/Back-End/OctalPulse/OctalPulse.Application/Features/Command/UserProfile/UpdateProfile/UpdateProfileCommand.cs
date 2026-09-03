using MediatR;
using OctalPulse.Application.Features.Query.UserProfile.GetProfile;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.UserProfile.UpdateProfile;

public record UpdateProfileCommand(
    Guid UserId,
    string Name,
    string? PhoneNumber,
    UserRole MainRole,
    string? ProfilePictureUrl) : IRequest<UserProfileResponse>;
