using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;

namespace OctalPulse.Application.Features.Command.Project.DeleteProject;

public class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand, Unit>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteProjectCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _unitOfWork.Projects.GetByIdWithTreeIncludingDeletedAsync(request.Id, cancellationToken);
        if (project is null)
            throw new NotFoundException("Project not found.");

        if (!project.IsDeleted)
        {
            var now = DateTime.UtcNow;
            project.IsDeleted = true;
            project.ModifiedDate = now;

            foreach (var member in project.Members)
            {
                member.IsDeleted = true;
                member.ModifiedDate = now;
                foreach (var role in member.Roles)
                    role.IsDeleted = true;
            }

            foreach (var track in project.Tracks)
            {
                track.IsDeleted = true;
                track.ModifiedDate = now;

                foreach (var trackMember in track.Members)
                {
                    trackMember.IsDeleted = true;
                    trackMember.ModifiedDate = now;
                }

                foreach (var majorTask in track.MajorTasks)
                {
                    majorTask.IsDeleted = true;
                    majorTask.ModifiedDate = now;

                    foreach (var minorTask in majorTask.MinorTasks)
                    {
                        minorTask.IsDeleted = true;
                        minorTask.ModifiedDate = now;
                    }
                }
            }

            foreach (var projectEvent in project.Events)
            {
                projectEvent.IsDeleted = true;
                projectEvent.ModifiedDate = now;
            }

            await _unitOfWork.CompleteAsync(cancellationToken);
        }

        return Unit.Value;
    }
}