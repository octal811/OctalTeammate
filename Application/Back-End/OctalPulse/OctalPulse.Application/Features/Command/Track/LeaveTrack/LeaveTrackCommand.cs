using MediatR;

namespace OctalPulse.Application.Features.Command.Track.LeaveTrack;

public record LeaveTrackCommand(
    Guid TrackId,
    Guid UserId) : IRequest<LeaveTrackResponse>;