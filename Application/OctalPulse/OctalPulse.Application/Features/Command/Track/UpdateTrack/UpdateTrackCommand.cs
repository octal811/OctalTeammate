using MediatR;

namespace OctalPulse.Application.Features.Command.Track.UpdateTrack;

public record UpdateTrackCommand(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description) : IRequest<UpdateTrackResponse>;