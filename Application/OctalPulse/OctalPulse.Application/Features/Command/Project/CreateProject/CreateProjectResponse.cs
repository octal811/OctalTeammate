using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Project.CreateProject;

public record CreateProjectResponse(
    Guid Id,
    string Title,
    string? Description,
    int Progress,
    ProjectStatus Status,
    DateTime CreatedDate);