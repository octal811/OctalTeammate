using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Track.ReviewTrackJoin;

public class ReviewTrackJoinCommandHandler : IRequestHandler<ReviewTrackJoinCommand, ReviewTrackJoinResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public ReviewTrackJoinCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ReviewTrackJoinResponse> Handle(
        ReviewTrackJoinCommand request,
        CancellationToken cancellationToken)
    {
        var track = await _unitOfWork.Tracks.GetByIdAsync(request.TrackId, cancellationToken);
        if (track is null)
            throw new NotFoundException("Track not found.");

        if (track.TrackLeadUserId != request.UserId)
            throw new ForbiddenException("Only the track creator can review join requests.");

        var member = await _unitOfWork.TrackMembers.FirstOrDefaultAsync(
            m => m.TrackId == request.TrackId && m.UserId == request.TargetUserId,
            cancellationToken);

        if (member is null)
            throw new NotFoundException("Join request not found.");

        if (member.Status == MembershipStatus.Approved)
            throw new ValidationException("This user is already a track member.");

        member.Status = request.Approve ? MembershipStatus.Approved : MembershipStatus.Rejected;
        member.ModifiedDate = DateTime.UtcNow;

        _unitOfWork.TrackMembers.Update(member);
        await _unitOfWork.CompleteAsync(cancellationToken);

        return new ReviewTrackJoinResponse(request.TrackId, request.TargetUserId, member.Status.ToString());
    }
}
