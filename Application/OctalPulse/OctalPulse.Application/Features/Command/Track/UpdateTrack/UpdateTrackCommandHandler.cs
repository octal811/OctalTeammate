using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Track.UpdateTrack;

public class UpdateTrackCommandHandler : IRequestHandler<UpdateTrackCommand, UpdateTrackResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProgressCalculator _progressCalculator;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public UpdateTrackCommandHandler(
        IUnitOfWork unitOfWork,
        IProgressCalculator progressCalculator,
        IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _progressCalculator = progressCalculator;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<UpdateTrackResponse> Handle(UpdateTrackCommand request, CancellationToken cancellationToken)
    {
        var track = await _unitOfWork.Tracks.GetByIdAsync(request.Id, cancellationToken);
        if (track is null)
            throw new NotFoundException("Track not found.");

        var isMember = await _unitOfWork.TrackMembers.AnyAsync(
            m => m.TrackId == track.Id && m.UserId == request.UserId && m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Only approved track members can update this track.");

        track.Name = request.Name;
        track.Description = request.Description;
        track.ModifiedDate = DateTime.UtcNow;

        _unitOfWork.Tracks.Update(track);
        await _unitOfWork.CompleteAsync(cancellationToken);

        var progress = await _progressCalculator.GetTrackProgressAsync(track.Id, cancellationToken);

        await _realtimeNotifier.TrackChangedAsync(track.Id, track.ProjectId, cancellationToken);

        return new UpdateTrackResponse(
            track.Id,
            track.ProjectId,
            track.Name,
            track.Description,
            progress,
            track.ModifiedDate);
    }
}