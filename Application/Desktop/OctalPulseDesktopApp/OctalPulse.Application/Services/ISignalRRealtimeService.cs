namespace OctalPulse.Application.Services;

public interface ISignalRRealtimeService : IAsyncDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(string accessToken, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task JoinProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task LeaveProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task JoinTrackAsync(Guid trackId, CancellationToken cancellationToken = default);
    Task LeaveTrackAsync(Guid trackId, CancellationToken cancellationToken = default);
    Task RegisterGroupMembershipAsync(IReadOnlyList<Guid> projectIds, IReadOnlyList<Guid> trackIds, CancellationToken cancellationToken = default);

    event Action<Guid>? ProjectChanged;
    event Action<Guid, Guid>? TrackChanged;
    event Action<Guid, Guid>? MajorTaskChanged;
    event Action<Guid, Guid>? MinorTaskChanged;
    event Action<Guid, Guid>? EventChanged;
    event Action<Guid, Guid?, Guid?>? PostCreated;
    event Action<Guid, Guid?, Guid?>? PostUpdated;
    event Action<Guid, Guid?, Guid?>? PostDeleted;
    event Action<Guid, Guid?>? PostReactionChanged;
    event Action<Guid, Guid, Guid?, Guid?>? CommentAdded;
    event Action<Guid, Guid, Guid?>? CommentDeleted;
    event Action<bool>? ConnectionStateChanged;
}
