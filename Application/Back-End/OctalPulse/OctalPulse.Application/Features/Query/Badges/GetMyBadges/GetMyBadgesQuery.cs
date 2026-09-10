using MediatR;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.Badges.GetMyBadges;

public record GetMyBadgesQuery(Guid UserId) : IRequest<GetMyBadgesResponse>;

public record BadgeProgressResponse(
    BadgeType Type,
    BadgeLevel? CurrentLevel,
    DateTime? AwardedDate,
    long CurrentValue,
    long NextTarget,
    int ProgressPercent);

public record GetMyBadgesResponse(IReadOnlyList<BadgeProgressResponse> Badges);