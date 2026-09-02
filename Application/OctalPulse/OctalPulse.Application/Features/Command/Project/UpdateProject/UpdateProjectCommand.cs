using MediatR;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Project.UpdateProject;

public record UpdateProjectCommand(
    Guid Id,
    string Title,
    string? Description,
    int Progress,
    ProjectStatus Status) : IRequest<UpdateProjectResponse>;