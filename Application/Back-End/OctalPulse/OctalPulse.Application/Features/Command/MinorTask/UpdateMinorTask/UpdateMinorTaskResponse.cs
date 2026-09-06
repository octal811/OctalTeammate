using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.MinorTask.UpdateMinorTask;

public record UpdateMinorTaskResponse(
    Guid Id,
    Guid MajorTaskId,
    string Title,
    string? Description,
    string? Target,
    MinorTaskState State,
    string? Notes,
    string? Link,
    int Order,
    long? WorkTimeSeconds,
    Guid? AssignedUserId,
    Guid? CreatedByUserId,
    DateTime? ModifiedDate);
