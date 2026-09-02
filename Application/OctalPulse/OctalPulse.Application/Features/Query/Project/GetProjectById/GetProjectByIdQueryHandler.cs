using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;

namespace OctalPulse.Application.Features.Query.Project.GetProjectById;

public class GetProjectByIdQueryHandler : IRequestHandler<GetProjectByIdQuery, GetProjectByIdResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetProjectByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetProjectByIdResponse> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
    {
        var project = await _unitOfWork.Projects.GetWithDetailsAsync(request.ProjectId, cancellationToken);
        if (project is null)
            throw new NotFoundException("Project not found.");

        var members = project.Members
            .Where(m => m.User is not null)
            .Select(m => new ProjectMemberItem(
                m.UserId,
                m.User.Name,
                m.User.Email ?? string.Empty))
            .ToList();

        return new GetProjectByIdResponse(
            project.Id,
            project.Title,
            project.Description,
            project.Progress,
            project.Status,
            project.CreatedDate,
            project.ModifiedDate,
            new ProjectCreator(
                project.CreatedByUserId,
                project.CreatedByUser.Name,
                project.CreatedByUser.Email ?? string.Empty),
            members.Count,
            members);
    }
}