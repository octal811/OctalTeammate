using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Infrastructure.Api;

namespace OctalPulse.Infrastructure.Services;

public class TaskService : ITaskService
{
    private readonly ApiClient _apiClient;

    public TaskService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // Major Tasks
    public Task<CreateMajorTaskResponse> CreateMajorTaskAsync(CreateMajorTaskRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<CreateMajorTaskResponse>(
            HttpMethod.Post,
            "/api/majortasks",
            request,
            cancellationToken);
    }

    public Task<GetMajorTasksByTrackResponse> GetMajorTasksByTrackAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<GetMajorTasksByTrackResponse>(
            HttpMethod.Get,
            "/api/majortasks/track",
            new GetMajorTasksByTrackRequest(trackId),
            cancellationToken);
    }

    public Task<UpdateMajorTaskResponse> UpdateMajorTaskAsync(UpdateMajorTaskRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<UpdateMajorTaskResponse>(
            HttpMethod.Put,
            "/api/majortasks",
            request,
            cancellationToken);
    }

    public Task DeleteMajorTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync(
            HttpMethod.Delete,
            "/api/majortasks",
            new DeleteMajorTaskRequest(id),
            cancellationToken);
    }

    // Minor Tasks
    public Task<CreateMinorTaskResponse> CreateMinorTaskAsync(CreateMinorTaskRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<CreateMinorTaskResponse>(
            HttpMethod.Post,
            "/api/minortasks",
            request,
            cancellationToken);
    }

    public Task<GetMinorTasksByMajorTaskResponse> GetMinorTasksByMajorTaskAsync(Guid majorTaskId, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<GetMinorTasksByMajorTaskResponse>(
            HttpMethod.Get,
            "/api/minortasks/major",
            new GetMinorTasksByMajorTaskRequest(majorTaskId),
            cancellationToken);
    }

    public Task<UpdateMinorTaskResponse> UpdateMinorTaskAsync(UpdateMinorTaskRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<UpdateMinorTaskResponse>(
            HttpMethod.Put,
            "/api/minortasks",
            request,
            cancellationToken);
    }

    public Task<AddMinorTaskWorkTimeResponse> AddMinorTaskWorkTimeAsync(AddMinorTaskWorkTimeRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<AddMinorTaskWorkTimeResponse>(
            HttpMethod.Post,
            "/api/minortasks/worktime",
            request,
            cancellationToken);
    }

    public Task DeleteMinorTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync(
            HttpMethod.Delete,
            "/api/minortasks",
            new DeleteMinorTaskRequest(id),
            cancellationToken);
    }
}
