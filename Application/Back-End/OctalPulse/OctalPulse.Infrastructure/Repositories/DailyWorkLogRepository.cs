using Microsoft.EntityFrameworkCore;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class DailyWorkLogRepository : BaseRepository<DailyWorkLog>, IDailyWorkLogRepository
{
    public DailyWorkLogRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<DailyWorkLog?> GetByUserAndDateAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(l => l.UserId == userId && l.WorkDate == date, cancellationToken);
    }

    public async Task<long> GetMaxSecondsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(l => l.UserId == userId)
            .MaxAsync(l => (long?)l.TotalSeconds, cancellationToken) ?? 0;
    }
}