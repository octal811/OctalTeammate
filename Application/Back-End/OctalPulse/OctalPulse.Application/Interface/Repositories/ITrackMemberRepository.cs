using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Interface.Repositories;

public interface ITrackMemberRepository : IGenericRepository<TrackMember>
{
    Task<TrackMember?> GetIncludingDeletedAsync(Guid trackId, Guid userId, CancellationToken cancellationToken = default);
}
