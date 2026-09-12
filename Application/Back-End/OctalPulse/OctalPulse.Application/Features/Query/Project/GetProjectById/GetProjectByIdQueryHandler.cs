using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.Project.GetProjectById;

public class GetProjectByIdQueryHandler : IRequestHandler<GetProjectByIdQuery, GetProjectByIdResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProgressCalculator _progressCalculator;

    public GetProjectByIdQueryHandler(IUnitOfWork unitOfWork, IProgressCalculator progressCalculator)
    {
        _unitOfWork = unitOfWork;
        _progressCalculator = progressCalculator;
    }

    public async Task<GetProjectByIdResponse> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
    {
        var project = await _unitOfWork.Projects.GetWithDetailsAsync(request.ProjectId, cancellationToken);
        if (project is null)
            throw new NotFoundException("Project not found.");

        var isMember = await _unitOfWork.ProjectMembers.AnyAsync(
            m => m.ProjectId == project.Id && m.UserId == request.UserId && m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Only approved project members can view this project.");

        var progress = await _progressCalculator.GetProjectProgressAsync(project.Id, cancellationToken);

        var members = project.Members
            .Where(m => m.Status == MembershipStatus.Approved && m.User is not null)
            .Select(m => new ProjectMemberItem(
                m.UserId,
                m.User.Name,
                m.User.Email ?? string.Empty))
            .ToList();

        var isOwner = project.CreatedByUserId == request.UserId;

        var pendingMembers = isOwner
            ? project.Members
                .Where(m => m.Status == MembershipStatus.Pending && m.User is not null)
                .Select(m => new ProjectMemberItem(
                    m.UserId,
                    m.User.Name,
                    m.User.Email ?? string.Empty))
                .ToList()
            : new List<ProjectMemberItem>();

        return new GetProjectByIdResponse(
            project.Id,
            project.Title,
            project.Description,
            progress,
            project.Status,
            project.CreatedDate,
            project.ModifiedDate,
            new ProjectCreator(
                project.CreatedByUserId,
                project.CreatedByUser.Name,
                project.CreatedByUser.Email ?? string.Empty),
            members.Count,
            members,
            pendingMembers);
    }
}