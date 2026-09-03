using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Event.UpdateEvent;

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
