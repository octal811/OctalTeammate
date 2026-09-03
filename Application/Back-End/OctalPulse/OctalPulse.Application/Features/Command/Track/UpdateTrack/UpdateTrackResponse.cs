namespace OctalPulse.Application.Features.Command.Track.UpdateTrack;

public record UpdateTrackResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    int Progress,
    DateTime? ModifiedDate);