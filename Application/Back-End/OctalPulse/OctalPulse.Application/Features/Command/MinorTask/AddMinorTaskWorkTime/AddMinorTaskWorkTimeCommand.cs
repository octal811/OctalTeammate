using MediatR;

namespace OctalPulse.Application.Features.Command.MinorTask.AddMinorTaskWorkTime;

public record AddMinorTaskWorkTimeCommand(
    Guid Id,
    long WorkTimeSeconds,
    Guid UserId) : IRequest<AddMinorTaskWorkTimeResponse>;