using MediatR;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Project.CreateProject;

public record CreateProjectCommand(
    string Title,
    string? Description,
    int Progress = 0,
    ProjectStatus Status = ProjectStatus.Active,
    Guid CreatedByUserId = default) : IRequest<CreateProjectResponse>;