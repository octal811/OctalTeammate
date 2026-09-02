using MediatR;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Project.CreateProject;

public record CreateProjectCommand(
    string Title,
    string? Description,
    ProjectStatus Status = ProjectStatus.Active,
    Guid CreatedByUserId = default) : IRequest<CreateProjectResponse>;