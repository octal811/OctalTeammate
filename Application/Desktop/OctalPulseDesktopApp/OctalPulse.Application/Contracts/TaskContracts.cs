using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Contracts;

// Major Task
public record CreateMajorTaskRequest(
    Guid TrackId,
    string Title,
    string? Description,
    string? Details,
    string? Link,
    MajorTaskState State,
    Priority Priority,
    DateTime? DueDate,
    int Order,
    Guid? AssignedUserId);

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

public record GetMajorTasksByTrackRequest(Guid TrackId);

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

public record GetMajorTasksByTrackResponse(IReadOnlyList<MajorTaskItem> MajorTasks);

public record UpdateMajorTaskRequest(
    Guid Id,
    string Title,
    string? Description,
    string? Details,
    string? Link,
    MajorTaskState State,
    Priority Priority,
    DateTime? DueDate,
    int Order,
    Guid? AssignedUserId);

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

public record DeleteMajorTaskRequest(Guid Id);

// Minor Task
public record CreateMinorTaskRequest(
    Guid MajorTaskId,
    string Title,
    string? Description,
    string? Target,
    MinorTaskState State,
    MinorTaskJobType? JobType,
    string? Notes,
    string? Link,
    int Order,
    Guid? AssignedUserId);

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
    string? CreatedByUserName,
    string? CreatedByUserEmail,
    bool IsDeleted,
    DateTime CreatedDate);

public record GetMinorTasksByMajorTaskRequest(Guid MajorTaskId);

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

public record GetMinorTasksByMajorTaskResponse(IReadOnlyList<MinorTaskItem> MinorTasks);

public record UpdateMinorTaskRequest(
    Guid Id,
    string? Title,
    string? Description,
    string? Target,
    MinorTaskState State,
    string? Notes,
    string? Link,
    int Order,
    Guid? AssignedUserId);

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

public record DeleteMinorTaskRequest(Guid Id);

public record AddMinorTaskWorkTimeRequest(
    Guid Id,
    long WorkTimeSeconds);

public record AddMinorTaskWorkTimeResponse(
    Guid Id,
    long WorkTimeSeconds);
