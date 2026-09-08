using MediatR;
using OctalPulse.Application.Features.Query.UserProfile.GetProfile;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.UserProfile.UpdateProfile;

public record UpdateProfileCommand(
    Guid UserId,
    string Name,
    UserRole MainRole,
    string? Bio,
    string? ProfilePictureUrl) : IRequest<UserProfileResponse>;
