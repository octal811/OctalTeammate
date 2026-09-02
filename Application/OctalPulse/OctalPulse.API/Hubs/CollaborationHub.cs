using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OctalPulse.Application.Interface.Repositories;

namespace OctalPulse.API.Hubs;

[Authorize]
public class CollaborationHub : Hub
{
    private readonly IUnitOfWork _unitOfWork;

    public CollaborationHub(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task JoinProject(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (await IsProjectMemberAsync(projectId, cancellationToken))
            await Groups.AddToGroupAsync(Context.ConnectionId, ProjectGroupName(projectId), cancellationToken);
    }

    public async Task LeaveProject(Guid projectId, CancellationToken cancellationToken = default)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ProjectGroupName(projectId), cancellationToken);
    }

    public async Task JoinTrack(Guid trackId, CancellationToken cancellationToken = default)
    {
        if (await IsTrackMemberAsync(trackId, cancellationToken))
            await Groups.AddToGroupAsync(Context.ConnectionId, TrackGroupName(trackId), cancellationToken);
    }

    public async Task LeaveTrack(Guid trackId, CancellationToken cancellationToken = default)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TrackGroupName(trackId), cancellationToken);
    }

    private async Task<bool> IsProjectMemberAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return false;

        return await _unitOfWork.ProjectMembers.AnyAsync(
            m => m.ProjectId == projectId && m.UserId == userId.Value && !m.IsDeleted,
            cancellationToken);
    }

    private async Task<bool> IsTrackMemberAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return false;

        return await _unitOfWork.TrackMembers.AnyAsync(
            m => m.TrackId == trackId && m.UserId == userId.Value && !m.IsDeleted,
            cancellationToken);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    internal static string ProjectGroupName(Guid projectId) => $"project-{projectId}";
    internal static string TrackGroupName(Guid trackId) => $"track-{trackId}";
}
