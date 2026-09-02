using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.MajorTask.GetMajorTasksByTrack;

public record GetMajorTasksByTrackResponse(IReadOnlyList<MajorTaskItem> MajorTasks);

public record MajorTaskItem(
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
    Guid? DeletedByUserId,
    DateTime CreatedDate);
