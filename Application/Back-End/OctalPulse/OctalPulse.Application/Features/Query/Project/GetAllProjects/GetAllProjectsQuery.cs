using MediatR;

namespace OctalPulse.Application.Features.Query.Project.GetAllProjects;

public record GetAllProjectsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    Guid UserId = default) : IRequest<GetAllProjectsResponse>;