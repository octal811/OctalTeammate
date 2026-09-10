using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
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
}
