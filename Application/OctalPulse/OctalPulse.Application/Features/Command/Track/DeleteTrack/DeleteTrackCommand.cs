using MediatR;

namespace OctalPulse.Application.Features.Command.Track.DeleteTrack;

public record DeleteTrackCommand(Guid Id) : IRequest<Unit>;