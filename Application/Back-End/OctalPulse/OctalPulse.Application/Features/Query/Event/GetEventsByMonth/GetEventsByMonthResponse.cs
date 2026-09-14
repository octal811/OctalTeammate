using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.Event.GetEventsByMonth;

public record GetEventsByMonthResponse(IReadOnlyList<EventItem> Events, int Year, int Month, int TotalCount);

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
    string? CreatedByUserName,
    string? CreatedByUserEmail,
    Guid? DeletedByUserId,
    bool IsDeleted,
    DateTime CreatedDate,
    DateTime? ModifiedDate);
