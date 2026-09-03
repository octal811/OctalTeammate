using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Contracts;

public record CreateProjectRequest(string Title, string? Description, ProjectStatus Status);

public record CreateProjectResponse(
    Guid Id,
    string Title,
    string? Description,
    int Progress,
    ProjectStatus Status,
    DateTime CreatedDate);

public record GetAllProjectsRequest(int PageNumber = 1, int PageSize = 20);

public record ProjectSummaryItem(
    Guid Id,
    string Title,
    string? Description,
    int Progress,
    ProjectStatus Status);

public record GetAllProjectsResponse(
    IReadOnlyList<ProjectSummaryItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public record GetProjectByIdRequest(Guid ProjectId);

public record ProjectCreator(
    Guid UserId,
    string Name,
    string Email);

public record ProjectMemberItem(
    Guid UserId,
    string Name,
    string Email);

public record GetProjectByIdResponse(
    Guid Id,
    string Title,
    string? Description,
    int Progress,
    ProjectStatus Status,
    DateTime CreatedDate,
    DateTime? ModifiedDate,
    ProjectCreator Creator,
    int MembersCount,
    IReadOnlyList<ProjectMemberItem> Members);

public record UpdateProjectRequest(
    Guid Id,
    string Title,
    string? Description,
    ProjectStatus Status);

public record UpdateProjectResponse(
    Guid Id,
    string Title,
    string? Description,
    int Progress,
    ProjectStatus Status,
    DateTime? ModifiedDate);

public record DeleteProjectRequest(Guid Id);

public record RequestProjectJoinRequest(Guid ProjectId, IReadOnlyList<ProjectRole> Roles);

public record RequestProjectJoinResponse(Guid ProjectId, string Status, string Message);

public record ReviewProjectJoinRequest(Guid ProjectId, Guid TargetUserId);

public record ReviewProjectJoinResponse(Guid ProjectId, Guid UserId, string Status);

public record UpdateProjectRolesRequest(Guid ProjectId, IReadOnlyList<ProjectRole> Roles);

public record UpdateProjectRolesResponse(Guid ProjectId, Guid UserId, IReadOnlyList<ProjectRole> Roles);
