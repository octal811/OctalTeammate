using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Application.Features.Command.Track.DeleteTrack;

public class DeleteTrackCommandHandler : IRequestHandler<DeleteTrackCommand, Unit>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public DeleteTrackCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<Unit> Handle(DeleteTrackCommand request, CancellationToken cancellationToken)
    {
        var track = await _unitOfWork.Tracks.GetByIdWithTreeIncludingDeletedAsync(request.Id, cancellationToken);
        if (track is null)
            throw new NotFoundException("Track not found.");

        var project = await _unitOfWork.Projects.GetByIdAsync(track.ProjectId, cancellationToken);

        var isTrackCreator = track.TrackLeadUserId == request.UserId;
        var isProjectCreator = project is not null && project.CreatedByUserId == request.UserId;

        if (!isTrackCreator && !isProjectCreator)
            throw new ForbiddenException("Only the track creator or the project creator can delete this track.");

        if (!track.IsDeleted)
        {
            var projectId = track.ProjectId;
            var now = DateTime.UtcNow;
            track.IsDeleted = true;
            track.ModifiedDate = now;

            foreach (var member in track.Members)
            {
                member.IsDeleted = true;
                member.ModifiedDate = now;
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

            await _unitOfWork.CompleteAsync(cancellationToken);

            await _realtimeNotifier.TrackChangedAsync(track.Id, projectId, cancellationToken);
        }

        return Unit.Value;
    }
}