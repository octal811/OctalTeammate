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

    public async Task PostCreatedAsync(Guid postId, Guid? projectId, Guid? trackId, CancellationToken cancellationToken = default)
    {
        if (projectId.HasValue)
        {
            await _hubContext.Clients
                .Group(CollaborationHub.ProjectGroupName(projectId.Value))
                .SendAsync("postCreated", postId, projectId.Value, trackId, cancellationToken);
        }
        else
        {
            await _hubContext.Clients
                .All
                .SendAsync("postCreated", postId, null, null, cancellationToken);
        }
    }

    public async Task PostUpdatedAsync(Guid postId, Guid? projectId, Guid? trackId, CancellationToken cancellationToken = default)
    {
        if (projectId.HasValue)
        {
            await _hubContext.Clients
                .Group(CollaborationHub.ProjectGroupName(projectId.Value))
                .SendAsync("postUpdated", postId, projectId.Value, trackId, cancellationToken);
        }
        else
        {
            await _hubContext.Clients
                .All
                .SendAsync("postUpdated", postId, null, null, cancellationToken);
        }
    }

    public async Task PostDeletedAsync(Guid postId, Guid? projectId, Guid? trackId, CancellationToken cancellationToken = default)
    {
        if (projectId.HasValue)
        {
            await _hubContext.Clients
                .Group(CollaborationHub.ProjectGroupName(projectId.Value))
                .SendAsync("postDeleted", postId, projectId.Value, trackId, cancellationToken);
        }
        else
        {
            await _hubContext.Clients
                .All
                .SendAsync("postDeleted", postId, null, null, cancellationToken);
        }
    }

    public async Task PostReactionChangedAsync(Guid postId, Guid? projectId, CancellationToken cancellationToken = default)
    {
        if (projectId.HasValue)
        {
            await _hubContext.Clients
                .Group(CollaborationHub.ProjectGroupName(projectId.Value))
                .SendAsync("postReactionChanged", postId, projectId.Value, cancellationToken);
        }
        else
        {
            await _hubContext.Clients
                .All
                .SendAsync("postReactionChanged", postId, null, cancellationToken);
        }
    }

    public async Task CommentAddedAsync(Guid postId, Guid commentId, Guid? parentCommentId, Guid? projectId, CancellationToken cancellationToken = default)
    {
        if (projectId.HasValue)
        {
            await _hubContext.Clients
                .Group(CollaborationHub.ProjectGroupName(projectId.Value))
                .SendAsync("commentAdded", postId, commentId, parentCommentId, projectId.Value, cancellationToken);
        }
        else
        {
            await _hubContext.Clients
                .All
                .SendAsync("commentAdded", postId, commentId, parentCommentId, null, cancellationToken);
        }
    }

    public async Task CommentDeletedAsync(Guid postId, Guid commentId, Guid? projectId, CancellationToken cancellationToken = default)
    {
        if (projectId.HasValue)
        {
            await _hubContext.Clients
                .Group(CollaborationHub.ProjectGroupName(projectId.Value))
                .SendAsync("commentDeleted", postId, commentId, projectId.Value, cancellationToken);
        }
        else
        {
            await _hubContext.Clients
                .All
                .SendAsync("commentDeleted", postId, commentId, null, cancellationToken);
        }
    }
}
