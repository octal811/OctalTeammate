using MediatR;

namespace OctalPulse.Application.Features.Query.UserProfile.GetProfile;

public record GetProfileQuery(Guid UserId) : IRequest<UserProfileResponse>;
