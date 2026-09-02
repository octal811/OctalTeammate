using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Event.CreateEvent;

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
