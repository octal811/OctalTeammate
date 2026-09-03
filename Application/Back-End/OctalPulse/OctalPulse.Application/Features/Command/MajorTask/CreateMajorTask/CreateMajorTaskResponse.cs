using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.MajorTask.CreateMajorTask;

public record CreateMajorTaskResponse(
    Guid Id,
    Guid TrackId,
    string Title,
    string? Description,
    string? Details,
    string? Link,
    MajorTaskState State,
    Priority Priority,
    DateTime? DueDate,
    int Order,
    Guid? AssignedUserId,
    int Progress,
    Guid? CreatedByUserId,
    DateTime CreatedDate);
