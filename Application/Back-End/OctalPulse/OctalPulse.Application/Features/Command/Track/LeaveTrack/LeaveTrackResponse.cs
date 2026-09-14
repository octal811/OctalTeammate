namespace OctalPulse.Application.Features.Command.Track.LeaveTrack;

public record LeaveTrackResponse(
    Guid TrackId,
    string Message);