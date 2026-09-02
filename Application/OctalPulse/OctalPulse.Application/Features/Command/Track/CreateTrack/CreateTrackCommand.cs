using MediatR;

namespace OctalPulse.Application.Features.Command.Track.CreateTrack;

public record CreateTrackCommand(
    Guid ProjectId,
    string Name,
    string? Description) : IRequest<CreateTrackResponse>;