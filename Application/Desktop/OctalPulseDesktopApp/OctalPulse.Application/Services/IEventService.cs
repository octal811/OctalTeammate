using OctalPulse.Application.Contracts;

namespace OctalPulse.Application.Services;

public interface IEventService
{
    Task<CreateEventResponse> CreateEventAsync(CreateEventRequest request, CancellationToken cancellationToken = default);
    Task<GetEventsByMonthResponse> GetEventsByMonthAsync(Guid projectId, int year, int month, CancellationToken cancellationToken = default);
    Task<UpdateEventResponse> UpdateEventAsync(UpdateEventRequest request, CancellationToken cancellationToken = default);
    Task DeleteEventAsync(Guid id, CancellationToken cancellationToken = default);
}
