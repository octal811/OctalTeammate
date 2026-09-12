using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Interface.Repositories;

public interface IUserBadgeRepository : IGenericRepository<UserBadge>
{
    Task<UserBadge?> GetByTypeAsync(Guid userId, Domain.Enums.BadgeType type, CancellationToken cancellationToken = default);
}

public interface IDailyWorkLogRepository : IGenericRepository<DailyWorkLog>
{
    Task<DailyWorkLog?> GetByUserAndDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken = default);
    Task<long> GetMaxSecondsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<long> GetTotalSecondsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetLongestActiveStreakAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<DailyWorkLog>> GetRangeAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}