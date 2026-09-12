using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Interface.Repositories;

public interface IMinorTaskRepository : IGenericRepository<MinorTask>
{
    Task<IReadOnlyList<MinorTask>> FindWithCreatedByUserAsync(
        System.Linq.Expressions.Expression<Func<MinorTask, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the total work time per day for tasks the user created and completed on the same day.
    /// Days are keyed by the UTC calendar date of <see cref="MinorTask.CompletedDate"/>.
    /// </summary>
    Task<IReadOnlyList<(DateOnly Day, long TotalSeconds)>> GetSameDayCompletionTotalsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
