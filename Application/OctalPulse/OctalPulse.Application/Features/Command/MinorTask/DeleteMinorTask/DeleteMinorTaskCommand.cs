using MediatR;

namespace OctalPulse.Application.Features.Command.MinorTask.DeleteMinorTask;

public record DeleteMinorTaskCommand(Guid Id, Guid UserId) : IRequest<Unit>;
