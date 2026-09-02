namespace OctalPulse.Application.Interface.Services;

public interface IProgressCalculator
{
    Task<int> GetTrackProgressAsync(Guid trackId, CancellationToken cancellationToken = default);
    Task<int> GetProjectProgressAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, int>> GetProjectsProgressAsync(
        IEnumerable<Guid> projectIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, int>> GetTracksProgressAsync(
        IEnumerable<Guid> trackIds,
        CancellationToken cancellationToken = default);
}