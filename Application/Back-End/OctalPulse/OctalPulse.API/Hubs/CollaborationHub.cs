using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace OctalPulse.API.Hubs;

[Authorize]
public class CollaborationHub : Hub
{
    private readonly ILogger<CollaborationHub> _logger;

    public CollaborationHub(ILogger<CollaborationHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinProject(Guid projectId)
    {
        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ProjectGroupName(projectId));
            _logger.LogInformation("Connection {ConnectionId} joined Project group {ProjectGroup}",
                Context.ConnectionId, ProjectGroupName(projectId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error joining project group {ProjectId}", projectId);
        }
    }

    public async Task LeaveProject(Guid projectId)
    {
        try
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, ProjectGroupName(projectId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error leaving project group {ProjectId}", projectId);
        }
    }

    public async Task JoinTrack(Guid trackId)
    {
        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, TrackGroupName(trackId));
            _logger.LogInformation("Connection {ConnectionId} joined Track group {TrackGroup}",
                Context.ConnectionId, TrackGroupName(trackId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error joining track group {TrackId}", trackId);
        }
    }

    public async Task LeaveTrack(Guid trackId)
    {
        try
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, TrackGroupName(trackId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error leaving track group {TrackId}", trackId);
        }
    }

    internal static string ProjectGroupName(Guid projectId) => $"project-{projectId}";
    internal static string TrackGroupName(Guid trackId) => $"track-{trackId}";
}
