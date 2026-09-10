using MediatR;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.MinorTask.UpdateMinorTask;

public record UpdateMinorTaskCommand(
    Guid Id,
    string Title,
    string? Description,
    string? Target,
    MinorTaskState State,
    MinorTaskJobType? JobType,
    string? Notes,
    string? Link,
    int Order,
    Guid? AssignedUserId,
    long? WorkTimeSeconds,
    Guid UserId) : IRequest<UpdateMinorTaskResponse>;
