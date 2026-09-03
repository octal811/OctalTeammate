using MediatR;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.MinorTask.CreateMinorTask;

public record CreateMinorTaskCommand(
    Guid MajorTaskId,
    string Title,
    string? Description,
    string? Target,
    MinorTaskState State,
    string? Notes,
    string? Link,
    int Order,
    Guid? AssignedUserId,
    Guid UserId) : IRequest<CreateMinorTaskResponse>;
