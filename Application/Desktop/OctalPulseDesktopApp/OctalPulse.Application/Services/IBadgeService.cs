using OctalPulse.Application.Contracts;

namespace OctalPulse.Application.Services;

public interface IBadgeService
{
    Task<GetMyBadgesResponse?> GetMyBadgesAsync(CancellationToken cancellationToken = default);
    Task<GetMyBadgesResponse?> GetBadgesAsync(Guid userId, CancellationToken cancellationToken = default);
}