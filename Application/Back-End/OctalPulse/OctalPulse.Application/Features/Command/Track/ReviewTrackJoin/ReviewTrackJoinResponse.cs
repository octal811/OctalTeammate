namespace OctalPulse.Application.Features.Command.Track.ReviewTrackJoin;

public record ReviewTrackJoinResponse(
    Guid TrackId,
    Guid UserId,
    string Status);
