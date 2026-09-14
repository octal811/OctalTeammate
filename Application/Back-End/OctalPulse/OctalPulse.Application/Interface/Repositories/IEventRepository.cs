using System.Linq.Expressions;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Interface.Repositories;

public interface IEventRepository : IGenericRepository<Event>
{
    Task<IReadOnlyList<Event>> FindWithCreatedByUserAsync(
        Expression<Func<Event, bool>> predicate,
        CancellationToken cancellationToken = default);
}
