using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Infrastructure.Api;

namespace OctalPulse.Infrastructure.Services;

public class TrackService : ITrackService
{
    private readonly ApiClient _apiClient;

    public TrackService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<CreateTrackResponse> CreateTrackAsync(CreateTrackRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<CreateTrackResponse>(
            HttpMethod.Post,
            "/api/tracks",
            request,
            cancellationToken);
    }

    public Task<UpdateTrackResponse> UpdateTrackAsync(UpdateTrackRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<UpdateTrackResponse>(
            HttpMethod.Put,
            "/api/tracks",
            request,
            cancellationToken);
    }

    public Task DeleteTrackAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync(
            HttpMethod.Delete,
            "/api/tracks",
            new DeleteTrackRequest(id),
            cancellationToken);
    }

    public Task<RequestTrackJoinResponse> JoinTrackAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<RequestTrackJoinResponse>(
            HttpMethod.Post,
            "/api/tracks/join",
            new RequestTrackJoinRequest(trackId),
            cancellationToken);
    }

    public Task<LeaveTrackResponse> LeaveTrackAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<LeaveTrackResponse>(
            HttpMethod.Post,
            "/api/tracks/leave",
            new LeaveTrackRequest(trackId),
            cancellationToken);
    }
}
