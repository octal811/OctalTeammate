using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Track.CreateTrack;

public class CreateTrackCommandHandler : IRequestHandler<CreateTrackCommand, CreateTrackResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly IBadgeService _badgeService;

    public CreateTrackCommandHandler(
        IUnitOfWork unitOfWork,
        IRealtimeNotifier realtimeNotifier,
        IBadgeService badgeService)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
        _badgeService = badgeService;
    }

    public async Task<CreateTrackResponse> Handle(CreateTrackCommand request, CancellationToken cancellationToken)
    {
        var project = await _unitOfWork.Projects.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project is null)
            throw new NotFoundException("Project not found.");

        var isProjectMember = await _unitOfWork.ProjectMembers.AnyAsync(
            m => m.ProjectId == project.Id &&
                 m.UserId == request.CreatedByUserId &&
                 m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isProjectMember)
            throw new ForbiddenException("Only approved project members can create tracks in this project.");

        var now = DateTime.UtcNow;

        var track = new Domain.Entities.Track
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            ProjectId = request.ProjectId,
            TrackLeadUserId = request.CreatedByUserId,
            IsDeleted = false,
            CreatedDate = now
        };

        await _unitOfWork.Tracks.AddAsync(track, cancellationToken);

        var leadMember = new TrackMember
        {
            Id = Guid.NewGuid(),
            TrackId = track.Id,
            UserId = request.CreatedByUserId,
            Status = MembershipStatus.Approved,
            IsDeleted = false,
            CreatedDate = now
        };

        await _unitOfWork.TrackMembers.AddAsync(leadMember, cancellationToken);

        await _badgeService.EvaluateTeamCaptainAsync(request.CreatedByUserId, true, cancellationToken);
        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.TrackChangedAsync(track.Id, track.ProjectId, cancellationToken);

        return new CreateTrackResponse(
            track.Id,
            track.ProjectId,
            track.Name,
            track.Description,
            0);
    }
}
