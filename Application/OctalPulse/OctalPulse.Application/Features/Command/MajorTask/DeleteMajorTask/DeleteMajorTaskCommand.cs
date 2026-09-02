using MediatR;

namespace OctalPulse.Application.Features.Command.MajorTask.DeleteMajorTask;

public record DeleteMajorTaskCommand(Guid Id, Guid UserId) : IRequest<Unit>;
