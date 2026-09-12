using System.Linq;
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

    public async Task<long> GetTotalSecondsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(l => l.UserId == userId)
            .SumAsync(l => (long?)l.TotalSeconds, cancellationToken) ?? 0;
    }

    public async Task<int> GetLongestActiveStreakAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var dates = (await _dbSet
                .Where(l => l.UserId == userId && l.TotalSeconds > 0)
                .Select(l => l.WorkDate)
                .ToListAsync(cancellationToken))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var best = 0;
        var run = 0;
        DateOnly? previous = null;

        foreach (var date in dates)
        {
            run = previous.HasValue && date == previous.Value.AddDays(1) ? run + 1 : 1;
            if (run > best) best = run;
            previous = date;
        }

        return best;
    }

    public async Task<IEnumerable<DailyWorkLog>> GetRangeAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(l => l.UserId == userId && l.WorkDate >= from && l.WorkDate <= to)
            .ToListAsync(cancellationToken);
    }
}