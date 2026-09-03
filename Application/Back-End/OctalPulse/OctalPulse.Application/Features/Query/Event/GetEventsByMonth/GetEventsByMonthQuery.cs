using MediatR;

namespace OctalPulse.Application.Features.Query.Event.GetEventsByMonth;

public record GetEventsByMonthQuery(
    Guid ProjectId,
    int Year,
    int Month,
    Guid UserId) : IRequest<GetEventsByMonthResponse>;
