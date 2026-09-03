using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Infrastructure.Api;

namespace OctalPulse.Infrastructure.Services;

public class ProjectService : IProjectService
{
    private readonly ApiClient _apiClient;

    public ProjectService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<CreateProjectResponse> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<CreateProjectResponse>(
            HttpMethod.Post,
            "/api/projects",
            request,
            cancellationToken);
    }

    public Task<GetAllProjectsResponse> GetAllProjectsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<GetAllProjectsResponse>(
            HttpMethod.Post,
            "/api/projects/list",
            new GetAllProjectsRequest(pageNumber, pageSize),
            cancellationToken);
    }

    public Task<GetProjectByIdResponse> GetProjectByIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<GetProjectByIdResponse>(
            HttpMethod.Get,
            "/api/projects",
            new GetProjectByIdRequest(projectId),
            cancellationToken);
    }

    public Task<GetTracksByProjectResponse> GetTracksByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<GetTracksByProjectResponse>(
            HttpMethod.Get,
            "/api/projects/tracks",
            new GetTracksByProjectRequest(projectId),
            cancellationToken);
    }

    public Task<UpdateProjectResponse> UpdateProjectAsync(UpdateProjectRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<UpdateProjectResponse>(
            HttpMethod.Put,
            "/api/projects",
            request,
            cancellationToken);
    }

    public Task DeleteProjectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync(
            HttpMethod.Delete,
            "/api/projects",
            new DeleteProjectRequest(id),
            cancellationToken);
    }

    public Task<RequestProjectJoinResponse> RequestJoinProjectAsync(RequestProjectJoinRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<RequestProjectJoinResponse>(
            HttpMethod.Post,
            "/api/projects/join",
            request,
            cancellationToken);
    }

    public Task<ReviewProjectJoinResponse> ApproveJoinProjectAsync(Guid projectId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<ReviewProjectJoinResponse>(
            HttpMethod.Post,
            "/api/projects/approve-join",
            new ReviewProjectJoinRequest(projectId, targetUserId),
            cancellationToken);
    }

    public Task<ReviewProjectJoinResponse> RejectJoinProjectAsync(Guid projectId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<ReviewProjectJoinResponse>(
            HttpMethod.Post,
            "/api/projects/reject-join",
            new ReviewProjectJoinRequest(projectId, targetUserId),
            cancellationToken);
    }

    public Task<UpdateProjectRolesResponse> UpdateProjectRolesAsync(UpdateProjectRolesRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<UpdateProjectRolesResponse>(
            HttpMethod.Put,
            "/api/projects/roles",
            request,
            cancellationToken);
    }
}
