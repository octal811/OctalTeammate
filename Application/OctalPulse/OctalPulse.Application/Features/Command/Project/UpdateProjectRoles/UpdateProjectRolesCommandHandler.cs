using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Project.UpdateProjectRoles;

public class UpdateProjectRolesCommandHandler : IRequestHandler<UpdateProjectRolesCommand, UpdateProjectRolesResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProjectRolesCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateProjectRolesResponse> Handle(
        UpdateProjectRolesCommand request,
        CancellationToken cancellationToken)
    {
        var project = await _unitOfWork.Projects.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project is null)
            throw new NotFoundException("Project not found.");

        var member = await _unitOfWork.ProjectMembers.FirstOrDefaultAsync(
            m => m.ProjectId == request.ProjectId && m.UserId == request.UserId,
            cancellationToken);

        if (member is null)
            throw new NotFoundException("You are not a member of this project.");

        if (member.Status != MembershipStatus.Approved)
            throw new ValidationException("You can only update roles for an approved membership.");

        var oldRoles = await _unitOfWork.UserProjectRoles.FindAsync(
            r => r.ProjectMemberId == member.Id,
            cancellationToken);

        foreach (var old in oldRoles)
            _unitOfWork.UserProjectRoles.Remove(old);

        var newRoles = request.Roles.Distinct().Select(r => new UserProjectRole
        {
            Id = Guid.NewGuid(),
            ProjectMemberId = member.Id,
            Role = r,
            IsDeleted = false
        });

        await _unitOfWork.UserProjectRoles.AddRangeAsync(newRoles, cancellationToken);
        await _unitOfWork.CompleteAsync(cancellationToken);

        return new UpdateProjectRolesResponse(
            request.ProjectId,
            request.UserId,
            request.Roles.Distinct().ToList());
    }
}
