using MediatR;
using OctalPulse.Application.Features.Query.UserProfile.GetProfile;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.UserProfile.SetUserImage;

public record SetUserImageCommand(
    Guid UserId,
    UserImageType ImageType,
    string ImageUrl) : IRequest<UserProfileResponse>;