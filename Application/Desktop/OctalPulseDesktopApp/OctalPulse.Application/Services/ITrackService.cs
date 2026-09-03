using OctalPulse.Application.Contracts;

namespace OctalPulse.Application.Services;

public interface ITrackService
{
    Task<CreateTrackResponse> CreateTrackAsync(CreateTrackRequest request, CancellationToken cancellationToken = default);
    Task<UpdateTrackResponse> UpdateTrackAsync(UpdateTrackRequest request, CancellationToken cancellationToken = default);
    Task DeleteTrackAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RequestTrackJoinResponse> RequestJoinTrackAsync(Guid trackId, CancellationToken cancellationToken = default);
    Task<ReviewTrackJoinResponse> ApproveJoinTrackAsync(Guid trackId, Guid targetUserId, CancellationToken cancellationToken = default);
    Task<ReviewTrackJoinResponse> RejectJoinTrackAsync(Guid trackId, Guid targetUserId, CancellationToken cancellationToken = default);
}
