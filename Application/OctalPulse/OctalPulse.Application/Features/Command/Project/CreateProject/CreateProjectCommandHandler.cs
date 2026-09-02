using MediatR;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Project.CreateProject;

public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, CreateProjectResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public CreateProjectCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<CreateProjectResponse> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var project = new Domain.Entities.Project
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Status = request.Status,
            CreatedByUserId = request.CreatedByUserId,
            IsDeleted = false,
            CreatedDate = now
        };

        await _unitOfWork.Projects.AddAsync(project, cancellationToken);

        var member = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = request.CreatedByUserId,
            Status = MembershipStatus.Approved,
            IsDeleted = false,
            CreatedDate = now
        };

        await _unitOfWork.ProjectMembers.AddAsync(member, cancellationToken);

        var memberRole = new UserProjectRole
        {
            Id = Guid.NewGuid(),
            ProjectMemberId = member.Id,
            Role = ProjectRole.ProjectManager,
            IsDeleted = false
        };

        await _unitOfWork.UserProjectRoles.AddAsync(memberRole, cancellationToken);

        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.ProjectChangedAsync(project.Id, cancellationToken);

        return new CreateProjectResponse(
            project.Id,
            project.Title,
            project.Description,
            0,
            project.Status,
            project.CreatedDate);
    }
}