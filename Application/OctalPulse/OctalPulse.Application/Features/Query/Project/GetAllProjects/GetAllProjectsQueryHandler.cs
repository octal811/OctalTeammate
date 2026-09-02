using MediatR;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Application.Features.Query.Project.GetAllProjects;

public class GetAllProjectsQueryHandler : IRequestHandler<GetAllProjectsQuery, GetAllProjectsResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProgressCalculator _progressCalculator;

    public GetAllProjectsQueryHandler(IUnitOfWork unitOfWork, IProgressCalculator progressCalculator)
    {
        _unitOfWork = unitOfWork;
        _progressCalculator = progressCalculator;
    }

    public async Task<GetAllProjectsResponse> Handle(GetAllProjectsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _unitOfWork.Projects.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            orderBy: q => q.OrderByDescending(p => p.CreatedDate),
            cancellationToken: cancellationToken);

        var projectIds = items.Select(p => p.Id).ToList();
        var progressMap = await _progressCalculator.GetProjectsProgressAsync(projectIds, cancellationToken);

        var summaries = items
            .Select(p => new ProjectSummaryItem(
                p.Id,
                p.Title,
                p.Description,
                progressMap.GetValueOrDefault(p.Id),
                p.Status))
            .ToList();

        return new GetAllProjectsResponse(summaries, totalCount, request.PageNumber, request.PageSize);
    }
}