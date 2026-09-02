using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Interface.Repositories;

public interface ITrackRepository : IGenericRepository<Track>
{
    Task<Track?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Track?> GetByIdWithTreeIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Track>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Track?> GetWithMajorTasksAsync(Guid id, CancellationToken cancellationToken = default);
}
