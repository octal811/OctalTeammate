using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.MinorTask.CreateMinorTask;

public record CreateMinorTaskResponse(
    Guid Id,
    Guid MajorTaskId,
    string Title,
    string? Description,
    string? Target,
    MinorTaskState State,
    MinorTaskJobType? JobType,
    string? Notes,
    string? Link,
    int Order,
    long? WorkTimeSeconds,
    Guid? AssignedUserId,
    Guid? CreatedByUserId,
    bool IsDeleted,
    DateTime CreatedDate);
