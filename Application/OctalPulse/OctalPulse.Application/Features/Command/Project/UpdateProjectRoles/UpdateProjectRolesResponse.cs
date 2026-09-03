using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Project.UpdateProjectRoles;

public record UpdateProjectRolesResponse(
    Guid ProjectId,
    Guid UserId,
    List<ProjectRole> Roles);