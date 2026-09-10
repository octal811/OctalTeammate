using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

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
        var projectExists = await _unitOfWork.Projects.AnyAsync(
            p => p.Id == request.ProjectId,
            cancellationToken);

        if (!projectExists)
            throw new NotFoundException("Project not found.");

        var isMember = await _unitOfWork.ProjectMembers.AnyAsync(
            m => m.ProjectId == request.ProjectId && m.UserId == request.UserId && m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Only approved project members can view tracks in this project.");

        var tracks = (await _unitOfWork.Tracks.FindWithMembersAsync(
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
                progressMap.GetValueOrDefault(t.Id),
                t.Members
                    .Where(m => m.Status == Domain.Enums.MembershipStatus.Approved)
                    .Select(m => new TrackMemberItem(
                        m.UserId,
                        m.User.Name,
                        m.User.ProfilePictureUrl))
                    .ToList()))
            .ToList();

        return new GetTracksByProjectResponse(items);
    }
}