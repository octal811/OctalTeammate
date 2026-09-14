using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class EventRepository : BaseRepository<Event>, IEventRepository
{
    public EventRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Event>> FindWithCreatedByUserAsync(
        Expression<Func<Event, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _context.Events
            .AsNoTracking()
            .Include(e => e.CreatedByUser)
            .Where(predicate)
            .ToListAsync(cancellationToken);
    }
}
