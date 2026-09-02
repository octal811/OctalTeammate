using MediatR;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Application.Features.Query.Track.GetTracksByProject;

public class GetTracksByProjectQueryHandler : IRequestHandler<GetTracksByProjectQuery, GetTracksByProjectResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProgressCalculator _progressCalculator;

    public GetTracksByProjectQueryHandler(IUnitOfWork unitOfWork, IProgressCalculator progressCalculator)
    {
        _unitOfWork = unitOfWork;
        _progressCalculator = progressCalculator;
    }

    public async Task<GetTracksByProjectResponse> Handle(GetTracksByProjectQuery request, CancellationToken cancellationToken)
    {
        var tracks = (await _unitOfWork.Tracks.FindAsync(
            t => t.ProjectId == request.ProjectId,
            cancellationToken)).ToList();

        var progressMap = await _progressCalculator.GetTracksProgressAsync(
            tracks.Select(t => t.Id),
            cancellationToken);

        var items = tracks
            .OrderByDescending(t => t.CreatedDate)
            .Select(t => new TrackSummaryItem(
                t.Id,
                t.Name,
                t.Description,
                progressMap.GetValueOrDefault(t.Id)))
            .ToList();

        return new GetTracksByProjectResponse(items);
    }
}