using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.Project.GetAllProjects;

public record GetAllProjectsResponse(
    IReadOnlyList<ProjectSummaryItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public record ProjectSummaryItem(
    Guid Id,
    string Title,
    string? Description,
    int Progress,
    ProjectStatus Status);