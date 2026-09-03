namespace OctalPulse.Application.Contracts;

public record CreateTrackRequest(Guid ProjectId, string Name, string? Description);

public record CreateTrackResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    int Progress);

public record GetTracksByProjectRequest(Guid ProjectId);

public record TrackSummaryItem(
    Guid Id,
    string Name,
    string? Description,
    int Progress);

public record GetTracksByProjectResponse(IReadOnlyList<TrackSummaryItem> Tracks);

public record UpdateTrackRequest(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description);

public record UpdateTrackResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    int Progress,
    DateTime? ModifiedDate);

public record DeleteTrackRequest(Guid Id);

public record RequestTrackJoinRequest(Guid TrackId);

public record RequestTrackJoinResponse(Guid TrackId, string Status, string Message);

public record ReviewTrackJoinRequest(Guid TrackId, Guid TargetUserId);

public record ReviewTrackJoinResponse(Guid TrackId, Guid UserId, string Status);
