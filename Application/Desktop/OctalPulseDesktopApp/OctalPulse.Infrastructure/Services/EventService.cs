using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Infrastructure.Api;

namespace OctalPulse.Infrastructure.Services;

public class EventService : IEventService
{
    private readonly ApiClient _apiClient;

    public EventService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<CreateEventResponse> CreateEventAsync(CreateEventRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<CreateEventResponse>(
            HttpMethod.Post,
            "/api/events",
            request,
            cancellationToken);
    }

    public Task<GetEventsByMonthResponse> GetEventsByMonthAsync(Guid projectId, int year, int month, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<GetEventsByMonthResponse>(
            HttpMethod.Get,
            "/api/events/month",
            new GetEventsByMonthRequest(projectId, year, month),
            cancellationToken);
    }

    public Task<UpdateEventResponse> UpdateEventAsync(UpdateEventRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<UpdateEventResponse>(
            HttpMethod.Put,
            "/api/events",
            request,
            cancellationToken);
    }

    public Task DeleteEventAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync(
            HttpMethod.Delete,
            "/api/events",
            new DeleteEventRequest(id),
            cancellationToken);
    }
}
