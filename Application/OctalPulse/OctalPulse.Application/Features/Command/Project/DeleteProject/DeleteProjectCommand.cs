using MediatR;

namespace OctalPulse.Application.Features.Command.Project.DeleteProject;

public record DeleteProjectCommand(Guid Id) : IRequest<Unit>;