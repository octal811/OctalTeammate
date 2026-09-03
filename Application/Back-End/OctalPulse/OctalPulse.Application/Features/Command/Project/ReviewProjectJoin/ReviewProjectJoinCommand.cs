using MediatR;

namespace OctalPulse.Application.Features.Command.Project.ReviewProjectJoin;

public record ReviewProjectJoinCommand(
    Guid ProjectId,
    Guid UserId,
    Guid TargetUserId,
    bool Approve) : IRequest<ReviewProjectJoinResponse>;
