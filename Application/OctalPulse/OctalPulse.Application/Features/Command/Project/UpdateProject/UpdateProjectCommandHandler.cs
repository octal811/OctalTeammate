using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Application.Features.Command.Project.UpdateProject;

public class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand, UpdateProjectResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProgressCalculator _progressCalculator;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public UpdateProjectCommandHandler(
        IUnitOfWork unitOfWork,
        IProgressCalculator progressCalculator,
        IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _progressCalculator = progressCalculator;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<UpdateProjectResponse> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _unitOfWork.Projects.GetByIdAsync(request.Id, cancellationToken);
        if (project is null)
            throw new NotFoundException("Project not found.");

        project.Title = request.Title;
        project.Description = request.Description;
        project.Status = request.Status;
        project.ModifiedDate = DateTime.UtcNow;

        _unitOfWork.Projects.Update(project);
        await _unitOfWork.CompleteAsync(cancellationToken);

        var progress = await _progressCalculator.GetProjectProgressAsync(project.Id, cancellationToken);

        await _realtimeNotifier.ProjectChangedAsync(project.Id, cancellationToken);

        return new UpdateProjectResponse(
            project.Id,
            project.Title,
            project.Description,
            progress,
            project.Status,
            project.ModifiedDate);
    }
}