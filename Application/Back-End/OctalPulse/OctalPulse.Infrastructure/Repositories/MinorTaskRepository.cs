using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class MinorTaskRepository : BaseRepository<MinorTask>, IMinorTaskRepository
{
    public MinorTaskRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<MinorTask>> FindWithCreatedByUserAsync(
        Expression<Func<MinorTask, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _context.MinorTasks
            .AsNoTracking()
            .Include(m => m.CreatedByUser)
            .Where(predicate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(DateOnly Day, long TotalSeconds)>> GetSameDayCompletionTotalsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var completed = await _context.MinorTasks
            .AsNoTracking()
            .Where(m => m.CreatedByUserId == userId
                && m.State == MinorTaskState.Done
                && !m.IsDeleted
                && m.WorkTime.HasValue
                && m.CompletedDate.HasValue)
            .Select(m => new
            {
                m.WorkTime,
                m.CreatedDate,
                m.CompletedDate
            })
            .ToListAsync(cancellationToken);

        return completed
            .Where(m => DateOnly.FromDateTime(m.CompletedDate!.Value.ToUniversalTime())
                == DateOnly.FromDateTime(m.CreatedDate.ToUniversalTime()))
            .GroupBy(m => DateOnly.FromDateTime(m.CompletedDate!.Value.ToUniversalTime()))
            .Select(g => (g.Key, g.Sum(m => (long)m.WorkTime!.Value.TotalSeconds)))
            .ToList();
    }
}
