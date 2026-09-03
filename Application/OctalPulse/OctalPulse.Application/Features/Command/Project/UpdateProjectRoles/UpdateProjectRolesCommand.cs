using MediatR;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Project.UpdateProjectRoles;

public record UpdateProjectRolesCommand(
    Guid ProjectId,
    Guid UserId,
    List<ProjectRole> Roles) : IRequest<UpdateProjectRolesResponse>;