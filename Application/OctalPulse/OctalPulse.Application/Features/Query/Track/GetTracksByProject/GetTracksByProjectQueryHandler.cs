using MediatR;
using OctalPulse.Application.Interface.Repositories;

namespace OctalPulse.Application.Features.Query.Track.GetTracksByProject;

public class GetTracksByProjectQueryHandler : IRequestHandler<GetTracksByProjectQuery, GetTracksByProjectResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetTracksByProjectQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetTracksByProjectResponse> Handle(GetTracksByProjectQuery request, CancellationToken cancellationToken)
    {
        var tracks = await _unitOfWork.Tracks.FindAsync(
            t => t.ProjectId == request.ProjectId,
            cancellationToken);

        var items = tracks
            .OrderByDescending(t => t.CreatedDate)
            .Select(t => new TrackSummaryItem(t.Id, t.Name, t.Description, t.Progress))
            .ToList();

        return new GetTracksByProjectResponse(items);
    }
}