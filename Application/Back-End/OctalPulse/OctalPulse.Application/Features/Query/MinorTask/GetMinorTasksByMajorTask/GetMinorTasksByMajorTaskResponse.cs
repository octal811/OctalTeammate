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
    string? Notes,
    string? Link,
    int Order,
    Guid? AssignedUserId,
    Guid? CreatedByUserId,
    bool IsDeleted,
    DateTime CreatedDate);
