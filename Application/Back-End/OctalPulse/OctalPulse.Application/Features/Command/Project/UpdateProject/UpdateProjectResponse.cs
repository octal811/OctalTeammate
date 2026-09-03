using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Project.UpdateProject;

public record UpdateProjectResponse(
    Guid Id,
    string Title,
    string? Description,
    int Progress,
    ProjectStatus Status,
    DateTime? ModifiedDate);