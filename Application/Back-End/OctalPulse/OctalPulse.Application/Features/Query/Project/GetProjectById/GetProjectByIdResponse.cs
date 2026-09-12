using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.Project.GetProjectById;

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
    IReadOnlyList<ProjectMemberItem> Members,
    IReadOnlyList<ProjectMemberItem> PendingMembers);

public record ProjectCreator(
    Guid UserId,
    string Name,
    string Email);

public record ProjectMemberItem(
    Guid UserId,
    string Name,
    string Email);