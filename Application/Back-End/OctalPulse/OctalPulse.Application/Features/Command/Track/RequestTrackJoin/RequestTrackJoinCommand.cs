using MediatR;

namespace OctalPulse.Application.Features.Command.Track.RequestTrackJoin;

public record RequestTrackJoinCommand(
    Guid TrackId,
    Guid UserId) : IRequest<RequestTrackJoinResponse>;
