using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Project.RequestProjectJoin;

public class RequestProjectJoinCommandHandler : IRequestHandler<RequestProjectJoinCommand, RequestProjectJoinResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public RequestProjectJoinCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<RequestProjectJoinResponse> Handle(
        RequestProjectJoinCommand request,
        CancellationToken cancellationToken)
    {
        var project = await _unitOfWork.Projects.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project is null)
            throw new NotFoundException("Project not found.");

        if (project.CreatedByUserId == request.UserId)
            throw new ValidationException("You created this project and are already a member.");

        var existing = await _unitOfWork.ProjectMembers.FirstOrDefaultAsync(
            m => m.ProjectId == request.ProjectId && m.UserId == request.UserId,
            cancellationToken);

        if (existing is null)
        {
            var member = new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                UserId = request.UserId,
                Status = MembershipStatus.Pending,
                IsDeleted = false,
                CreatedDate = DateTime.UtcNow
            };

            await _unitOfWork.ProjectMembers.AddAsync(member, cancellationToken);
            await _unitOfWork.CompleteAsync(cancellationToken);

            if (request.Roles is { Count: > 0 })
            {
                var roles = request.Roles.Distinct().Select(r => new UserProjectRole
                {
                    Id = Guid.NewGuid(),
                    ProjectMemberId = member.Id,
                    Role = r,
                    IsDeleted = false
                });

                await _unitOfWork.UserProjectRoles.AddRangeAsync(roles, cancellationToken);
                await _unitOfWork.CompleteAsync(cancellationToken);
            }

            return new RequestProjectJoinResponse(request.ProjectId, "Pending",
                "Your request to join has been sent. Please wait for the project creator to accept.");
        }

        if (existing.Status == MembershipStatus.Approved)
            throw new ValidationException("You are already a member of this project.");

        existing.Status = MembershipStatus.Pending;
        existing.ModifiedDate = DateTime.UtcNow;
        _unitOfWork.ProjectMembers.Update(existing);
        await _unitOfWork.CompleteAsync(cancellationToken);

        if (request.Roles is { Count: > 0 })
        {
            var oldRoles = await _unitOfWork.UserProjectRoles.FindAsync(
                r => r.ProjectMemberId == existing.Id,
                cancellationToken);

            foreach (var old in oldRoles)
                _unitOfWork.UserProjectRoles.Remove(old);

            var newRoles = request.Roles.Distinct().Select(r => new UserProjectRole
            {
                Id = Guid.NewGuid(),
                ProjectMemberId = existing.Id,
                Role = r,
                IsDeleted = false
            });

            await _unitOfWork.UserProjectRoles.AddRangeAsync(newRoles, cancellationToken);
            await _unitOfWork.CompleteAsync(cancellationToken);
        }

        return new RequestProjectJoinResponse(request.ProjectId, "Pending",
            "Your request to join has been re-sent. Please wait for the project creator to accept.");
    }
}
