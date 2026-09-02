using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.MajorTask.UpdateMajorTask;

public record UpdateMajorTaskResponse(
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
    DateTime? ModifiedDate);
