using MediatR;
using OctalPulse.Application.Interface.Repositories;

namespace OctalPulse.Application.Features.Query.Project.GetAllProjects;

public class GetAllProjectsQueryHandler : IRequestHandler<GetAllProjectsQuery, GetAllProjectsResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAllProjectsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetAllProjectsResponse> Handle(GetAllProjectsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _unitOfWork.Projects.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            orderBy: q => q.OrderByDescending(p => p.CreatedDate),
            cancellationToken: cancellationToken);

        var summaries = items
            .Select(p => new ProjectSummaryItem(p.Id, p.Title, p.Description, p.Progress, p.Status))
            .ToList();

        return new GetAllProjectsResponse(summaries, totalCount, request.PageNumber, request.PageSize);
    }
}