using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Track.RequestTrackJoin;

public class RequestTrackJoinCommandHandler : IRequestHandler<RequestTrackJoinCommand, RequestTrackJoinResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public RequestTrackJoinCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
            throw new ForbiddenException("Only approved project members can request to join a track in this project.");

        if (track.TrackLeadUserId == request.UserId)
            throw new ValidationException("You created this track and are already its member.");

        var existing = await _unitOfWork.TrackMembers.FirstOrDefaultAsync(
            m => m.TrackId == request.TrackId && m.UserId == request.UserId,
            cancellationToken);

        if (existing is null)
        {
            var member = new TrackMember
            {
                Id = Guid.NewGuid(),
                TrackId = request.TrackId,
                UserId = request.UserId,
                Status = MembershipStatus.Pending,
                IsDeleted = false,
                CreatedDate = DateTime.UtcNow
            };

            await _unitOfWork.TrackMembers.AddAsync(member, cancellationToken);
            await _unitOfWork.CompleteAsync(cancellationToken);

            return new RequestTrackJoinResponse(request.TrackId, "Pending",
                "Your request to join has been sent. Please wait for the track creator to accept.");
        }

        if (existing.Status == MembershipStatus.Approved)
            throw new ValidationException("You are already a member of this track.");

        existing.Status = MembershipStatus.Pending;
        existing.ModifiedDate = DateTime.UtcNow;
        _unitOfWork.TrackMembers.Update(existing);
        await _unitOfWork.CompleteAsync(cancellationToken);

        return new RequestTrackJoinResponse(request.TrackId, "Pending",
            "Your request to join has been re-sent. Please wait for the track creator to accept.");
    }
}
