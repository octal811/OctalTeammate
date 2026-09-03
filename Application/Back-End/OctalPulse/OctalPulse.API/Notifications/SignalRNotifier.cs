using Microsoft.AspNetCore.SignalR;
using OctalPulse.API.Hubs;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.API.Notifications;

public class SignalRNotifier : IRealtimeNotifier
{
    private readonly IHubContext<CollaborationHub> _hubContext;

    public SignalRNotifier(IHubContext<CollaborationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task ProjectChangedAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients
            .Group(CollaborationHub.ProjectGroupName(projectId))
            .SendAsync("projectChanged", projectId, cancellationToken);
    }

    public async Task TrackChangedAsync(Guid trackId, Guid projectId, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients
            .Group(CollaborationHub.ProjectGroupName(projectId))
            .SendAsync("trackChanged", trackId, projectId, cancellationToken);
    }

    public async Task MajorTaskChangedAsync(Guid trackId, Guid majorTaskId, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients
            .Group(CollaborationHub.TrackGroupName(trackId))
            .SendAsync("majorTaskChanged", majorTaskId, trackId, cancellationToken);
    }

    public async Task MinorTaskChangedAsync(Guid trackId, Guid minorTaskId, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients
            .Group(CollaborationHub.TrackGroupName(trackId))
            .SendAsync("minorTaskChanged", minorTaskId, trackId, cancellationToken);
    }

    public async Task EventChangedAsync(Guid projectId, Guid eventId, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients
            .Group(CollaborationHub.ProjectGroupName(projectId))
            .SendAsync("eventChanged", eventId, projectId, cancellationToken);
    }
}
