using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Interface.Repositories;

public interface IMinorTaskRepository : IGenericRepository<MinorTask>
{
    Task<IReadOnlyList<MinorTask>> FindWithCreatedByUserAsync(
        System.Linq.Expressions.Expression<Func<MinorTask, bool>> predicate,
        CancellationToken cancellationToken = default);
}
