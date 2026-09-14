using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Track.LeaveTrack;

public class LeaveTrackCommandHandler : IRequestHandler<LeaveTrackCommand, LeaveTrackResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public LeaveTrackCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<LeaveTrackResponse> Handle(LeaveTrackCommand request, CancellationToken cancellationToken)
    {
        var track = await _unitOfWork.Tracks.GetByIdAsync(request.TrackId, cancellationToken);
        if (track is null)
            throw new NotFoundException("Track not found.");

        if (track.TrackLeadUserId == request.UserId)
            throw new ForbiddenException("The track creator cannot leave this track.");

        var membership = await _unitOfWork.TrackMembers.FirstOrDefaultAsync(
            m => m.TrackId == request.TrackId && m.UserId == request.UserId,
            cancellationToken);

        if (membership is null || membership.Status != MembershipStatus.Approved)
            throw new ValidationException("You are not a member of this track.");

        membership.IsDeleted = true;
        membership.ModifiedDate = DateTime.UtcNow;
        _unitOfWork.TrackMembers.Update(membership);

        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.TrackChangedAsync(track.Id, track.ProjectId, cancellationToken);

        return new LeaveTrackResponse(request.TrackId, "You have left the track.");
    }
}