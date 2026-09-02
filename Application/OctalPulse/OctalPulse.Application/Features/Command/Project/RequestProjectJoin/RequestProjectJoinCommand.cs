using MediatR;

namespace OctalPulse.Application.Features.Command.Project.RequestProjectJoin;

public record RequestProjectJoinCommand(
    Guid ProjectId,
    Guid UserId) : IRequest<RequestProjectJoinResponse>;
