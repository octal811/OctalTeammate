using MediatR;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Event.UpdateEvent;

public record UpdateEventCommand(
    Guid Id,
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
    Guid UserId) : IRequest<UpdateEventResponse>;
