using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Contracts;

public record CreateEventRequest(
    Guid ProjectId,
    string Title,
    string? Description,
    EventType Type,
    DateTime StartDate,
    DateTime? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool IsAllDay,
    Guid? TrackId,
    Guid? MajorTaskId);

public record CreateEventResponse(
    Guid Id,
    Guid? ProjectId,
    string Title,
    string? Description,
    EventType Type,
    DateTime StartDate,
    DateTime? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool IsAllDay,
    Guid? TrackId,
    Guid? MajorTaskId,
    Guid CreatedByUserId,
    bool IsDeleted,
    DateTime CreatedDate);

public record GetEventsByMonthRequest(Guid ProjectId, int Year, int Month);

public record EventItem(
    Guid Id,
    Guid? ProjectId,
    string Title,
    string? Description,
    EventType Type,
    DateTime StartDate,
    DateTime? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool IsAllDay,
    Guid? TrackId,
    Guid? MajorTaskId,
    Guid CreatedByUserId,
    Guid? DeletedByUserId,
    bool IsDeleted,
    DateTime CreatedDate,
    DateTime? ModifiedDate);

public record GetEventsByMonthResponse(
    IReadOnlyList<EventItem> Events,
    int Year,
    int Month,
    int TotalCount);

public record UpdateEventRequest(
    Guid Id,
    string Title,
    string? Description,
    EventType Type,
    DateTime StartDate,
    DateTime? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool IsAllDay);

public record UpdateEventResponse(
    Guid Id,
    Guid? ProjectId,
    string Title,
    string? Description,
    EventType Type,
    DateTime StartDate,
    DateTime? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool IsAllDay,
    Guid? TrackId,
    Guid? MajorTaskId,
    Guid CreatedByUserId,
    DateTime? ModifiedDate);

public record DeleteEventRequest(Guid Id);
