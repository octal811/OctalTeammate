using MediatR;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.MajorTask.UpdateMajorTask;

public record UpdateMajorTaskCommand(
    Guid Id,
    string Title,
    string? Description,
    string? Details,
    string? Link,
    MajorTaskState State,
    Priority Priority,
    DateTime? DueDate,
    int Order,
    Guid? AssignedUserId,
    Guid UserId) : IRequest<UpdateMajorTaskResponse>;
