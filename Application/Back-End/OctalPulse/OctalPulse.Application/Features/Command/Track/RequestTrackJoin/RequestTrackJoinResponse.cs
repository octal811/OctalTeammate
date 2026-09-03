namespace OctalPulse.Application.Features.Command.Track.RequestTrackJoin;

public record RequestTrackJoinResponse(
    Guid TrackId,
    string Status,
    string Message);
