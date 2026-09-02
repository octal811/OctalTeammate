namespace OctalPulse.Application.Interface.Services;

public interface IRealtimeNotifier
{
    Task ProjectChangedAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task TrackChangedAsync(Guid trackId, Guid projectId, CancellationToken cancellationToken = default);
    Task MajorTaskChangedAsync(Guid trackId, Guid majorTaskId, CancellationToken cancellationToken = default);
    Task MinorTaskChangedAsync(Guid trackId, Guid minorTaskId, CancellationToken cancellationToken = default);
}