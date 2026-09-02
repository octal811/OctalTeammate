using MediatR;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Project.CreateProject;

public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, CreateProjectResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateProjectCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateProjectResponse> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var project = new Domain.Entities.Project
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Progress = 0,
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

        return new CreateProjectResponse(
            project.Id,
            project.Title,
            project.Description,
            project.Progress,
            project.Status,
            project.CreatedDate);
    }
}