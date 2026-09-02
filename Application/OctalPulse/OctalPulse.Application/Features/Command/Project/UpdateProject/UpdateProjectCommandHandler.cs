using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;

namespace OctalPulse.Application.Features.Command.Project.UpdateProject;

public class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand, UpdateProjectResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProjectCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateProjectResponse> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _unitOfWork.Projects.GetByIdAsync(request.Id, cancellationToken);
        if (project is null)
            throw new NotFoundException("Project not found.");

        project.Title = request.Title;
        project.Description = request.Description;
        project.Progress = request.Progress;
        project.Status = request.Status;
        project.ModifiedDate = DateTime.UtcNow;

        _unitOfWork.Projects.Update(project);
        await _unitOfWork.CompleteAsync(cancellationToken);

        return new UpdateProjectResponse(
            project.Id,
            project.Title,
            project.Description,
            project.Progress,
            project.Status,
            project.ModifiedDate);
    }
}