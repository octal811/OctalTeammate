using MediatR;

namespace OctalPulse.Application.Features.Command.Track.ReviewTrackJoin;

public record ReviewTrackJoinCommand(
    Guid TrackId,
    Guid UserId,
    Guid TargetUserId,
    bool Approve) : IRequest<ReviewTrackJoinResponse>;
