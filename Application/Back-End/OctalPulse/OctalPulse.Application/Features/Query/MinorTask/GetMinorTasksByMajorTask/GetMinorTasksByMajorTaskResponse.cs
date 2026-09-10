using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.MinorTask.GetMinorTasksByMajorTask;

public record GetMinorTasksByMajorTaskResponse(IReadOnlyList<MinorTaskItem> MinorTasks);

public record MinorTaskItem(
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
    string? CreatedByUserName,
    string? CreatedByUserEmail,
    bool IsDeleted,
    DateTime CreatedDate);
