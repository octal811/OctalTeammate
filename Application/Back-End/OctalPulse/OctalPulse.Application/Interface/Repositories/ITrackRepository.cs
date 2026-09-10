using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Interface.Repositories;

public interface ITrackRepository : IGenericRepository<Track>
{
    Task<Track?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Track?> GetByIdWithTreeIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Track?> GetWithMajorTasksAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Track>> GetByIdsWithMajorTasksAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Track>> GetByProjectIdsWithMajorTasksAsync(
        IEnumerable<Guid> projectIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Track>> FindWithMembersAsync(
        System.Linq.Expressions.Expression<Func<Track, bool>> predicate,
        CancellationToken cancellationToken = default);
}
