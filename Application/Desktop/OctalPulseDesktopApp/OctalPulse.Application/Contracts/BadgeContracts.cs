namespace OctalPulse.Application.Contracts;

public record BadgeProgressResponse(
    string Type,
    string? CurrentLevel,
    DateTime? AwardedDate,
    long CurrentValue,
    long NextTarget,
    int ProgressPercent);

public record GetMyBadgesResponse(IReadOnlyList<BadgeProgressResponse> Badges);