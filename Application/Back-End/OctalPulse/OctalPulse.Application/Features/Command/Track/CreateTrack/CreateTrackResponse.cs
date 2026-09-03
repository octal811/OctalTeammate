namespace OctalPulse.Application.Features.Command.Track.CreateTrack;

public record CreateTrackResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    int Progress);