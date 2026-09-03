using MediatR;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Project.RequestProjectJoin;

public record RequestProjectJoinCommand(
    Guid ProjectId,
    Guid UserId,
    List<ProjectRole> Roles) : IRequest<RequestProjectJoinResponse>;
