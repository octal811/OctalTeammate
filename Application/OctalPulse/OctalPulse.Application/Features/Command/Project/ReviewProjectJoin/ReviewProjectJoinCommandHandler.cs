using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Project.ReviewProjectJoin;

public class ReviewProjectJoinCommandHandler : IRequestHandler<ReviewProjectJoinCommand, ReviewProjectJoinResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public ReviewProjectJoinCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ReviewProjectJoinResponse> Handle(
        ReviewProjectJoinCommand request,
        CancellationToken cancellationToken)
    {
        var project = await _unitOfWork.Projects.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project is null)
            throw new NotFoundException("Project not found.");

        if (project.CreatedByUserId != request.UserId)
            throw new ForbiddenException("Only the project creator can review join requests.");

        var member = await _unitOfWork.ProjectMembers.FirstOrDefaultAsync(
            m => m.ProjectId == request.ProjectId && m.UserId == request.TargetUserId,
            cancellationToken);

        if (member is null)
            throw new NotFoundException("Join request not found.");

        if (member.Status == MembershipStatus.Approved)
            throw new ValidationException("This user is already a project member.");

        member.Status = request.Approve ? MembershipStatus.Approved : MembershipStatus.Rejected;
        member.ModifiedDate = DateTime.UtcNow;

        _unitOfWork.ProjectMembers.Update(member);
        await _unitOfWork.CompleteAsync(cancellationToken);

        return new ReviewProjectJoinResponse(request.ProjectId, request.TargetUserId, member.Status.ToString());
    }
}
