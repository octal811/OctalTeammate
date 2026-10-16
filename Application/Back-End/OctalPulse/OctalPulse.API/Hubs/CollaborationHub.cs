using System.Security.Claims;
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

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            var userGroup = UserGroupName(userId.Value);
            await Groups.AddToGroupAsync(Context.ConnectionId, userGroup);
            _logger.LogInformation("SignalR Connection {ConnectionId} auto-joined User group {UserGroup}",
                Context.ConnectionId, userGroup);
        }
        else
        {
            _logger.LogWarning("SignalR Connection {ConnectionId} connected without valid UserId claim.", Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            var userGroup = UserGroupName(userId.Value);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userGroup);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinUser(Guid userId)
    {
        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroupName(userId));
            _logger.LogInformation("Connection {ConnectionId} explicitly joined User group {UserGroup}",
                Context.ConnectionId, UserGroupName(userId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error joining user group {UserId}", userId);
        }
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

    private Guid? GetCurrentUserId()
    {
        var idClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst("nameid")?.Value
            ?? Context.UserIdentifier;

        return Guid.TryParse(idClaim, out var id) ? id : null;
    }

    internal static string ProjectGroupName(Guid projectId) => $"project-{projectId}";
    internal static string TrackGroupName(Guid trackId) => $"track-{trackId}";
    internal static string UserGroupName(Guid userId) => $"user-{userId}";
}
