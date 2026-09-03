using OctalPulse.Application.Contracts;

namespace OctalPulse.Application.Services;

public interface IProjectService
{
    Task<CreateProjectResponse> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);
    Task<GetAllProjectsResponse> GetAllProjectsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<GetProjectByIdResponse> GetProjectByIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<GetTracksByProjectResponse> GetTracksByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<UpdateProjectResponse> UpdateProjectAsync(UpdateProjectRequest request, CancellationToken cancellationToken = default);
    Task DeleteProjectAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RequestProjectJoinResponse> RequestJoinProjectAsync(RequestProjectJoinRequest request, CancellationToken cancellationToken = default);
    Task<ReviewProjectJoinResponse> ApproveJoinProjectAsync(Guid projectId, Guid targetUserId, CancellationToken cancellationToken = default);
    Task<ReviewProjectJoinResponse> RejectJoinProjectAsync(Guid projectId, Guid targetUserId, CancellationToken cancellationToken = default);
    Task<UpdateProjectRolesResponse> UpdateProjectRolesAsync(UpdateProjectRolesRequest request, CancellationToken cancellationToken = default);
}
