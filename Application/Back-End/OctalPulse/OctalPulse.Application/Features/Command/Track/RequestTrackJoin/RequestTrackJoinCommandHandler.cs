using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Track.RequestTrackJoin;

public class RequestTrackJoinCommandHandler : IRequestHandler<RequestTrackJoinCommand, RequestTrackJoinResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public RequestTrackJoinCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<RequestTrackJoinResponse> Handle(
        RequestTrackJoinCommand request,
        CancellationToken cancellationToken)
    {
        var track = await _unitOfWork.Tracks.GetByIdAsync(request.TrackId, cancellationToken);
        if (track is null)
            throw new NotFoundException("Track not found.");

        var isProjectMember = await _unitOfWork.ProjectMembers.AnyAsync(
            m => m.ProjectId == track.ProjectId && m.UserId == request.UserId && m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isProjectMember)
            throw new ForbiddenException("Only approved project members can join a track in this project.");

        if (track.TrackLeadUserId == request.UserId)
            throw new ValidationException("You created this track and are already its member.");

        var existing = await _unitOfWork.TrackMembers.GetIncludingDeletedAsync(
            request.TrackId,
            request.UserId,
            cancellationToken);

        if (existing is null)
        {
            var member = new TrackMember
            {
                Id = Guid.NewGuid(),
                TrackId = request.TrackId,
                UserId = request.UserId,
                Status = MembershipStatus.Approved,
                IsDeleted = false,
                CreatedDate = DateTime.UtcNow
            };

            await _unitOfWork.TrackMembers.AddAsync(member, cancellationToken);
            await _unitOfWork.CompleteAsync(cancellationToken);

            await _realtimeNotifier.TrackChangedAsync(track.Id, track.ProjectId, cancellationToken);

            return new RequestTrackJoinResponse(request.TrackId, "Approved",
                "You have joined the track.");
        }

        if (!existing.IsDeleted && existing.Status == MembershipStatus.Approved)
            throw new ValidationException("You are already a member of this track.");

        existing.IsDeleted = false;
        existing.Status = MembershipStatus.Approved;
        existing.ModifiedDate = DateTime.UtcNow;
        _unitOfWork.TrackMembers.Update(existing);
        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.TrackChangedAsync(track.Id, track.ProjectId, cancellationToken);

        return new RequestTrackJoinResponse(request.TrackId, "Approved",
            "You have joined the track.");
    }
}