namespace OctalPulse.Application.Interface.Services;

public interface IRealtimeNotifier
{
    Task ProjectChangedAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task TrackChangedAsync(Guid trackId, Guid projectId, CancellationToken cancellationToken = default);
    Task MajorTaskChangedAsync(Guid trackId, Guid majorTaskId, CancellationToken cancellationToken = default);
    Task MinorTaskChangedAsync(Guid trackId, Guid minorTaskId, CancellationToken cancellationToken = default);
    Task EventChangedAsync(Guid projectId, Guid eventId, CancellationToken cancellationToken = default);
    Task PostCreatedAsync(Guid postId, Guid? projectId, Guid? trackId, CancellationToken cancellationToken = default);
    Task PostUpdatedAsync(Guid postId, Guid? projectId, Guid? trackId, CancellationToken cancellationToken = default);
    Task PostDeletedAsync(Guid postId, Guid? projectId, Guid? trackId, CancellationToken cancellationToken = default);
    Task PostReactionChangedAsync(Guid postId, Guid? projectId, CancellationToken cancellationToken = default);
    Task CommentAddedAsync(Guid postId, Guid commentId, Guid? parentCommentId, Guid? projectId, CancellationToken cancellationToken = default);
    Task CommentDeletedAsync(Guid postId, Guid commentId, Guid? projectId, CancellationToken cancellationToken = default);
}