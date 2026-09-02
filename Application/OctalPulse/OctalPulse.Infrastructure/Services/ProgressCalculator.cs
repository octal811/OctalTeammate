using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Infrastructure.Services;

public class ProgressCalculator : IProgressCalculator
{
    public async Task<int> RecalculateTrackProgressAsync(Guid trackId, IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var track = await unitOfWork.Tracks.GetWithMajorTasksAsync(trackId, cancellationToken);
        if (track is null)
            return 0;

        if (track.MajorTasks.Count == 0)
        {
            track.Progress = 0;
        }
        else
        {
            track.Progress = (int)track.MajorTasks.Average(mt => mt.Progress);
        }
        track.ModifiedDate = DateTime.UtcNow;

        unitOfWork.Tracks.Update(track);
        return track.Progress;
    }

    public async Task<int> RecalculateProjectProgressAsync(Guid projectId, IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var project = await unitOfWork.Projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
            return 0;

        var tracks = await unitOfWork.Tracks.GetByProjectIdAsync(projectId, cancellationToken);
        if (tracks.Count == 0)
        {
            project.Progress = 0;
        }
        else
        {
            project.Progress = (int)tracks.Average(t => t.Progress);
        }
        project.ModifiedDate = DateTime.UtcNow;

        unitOfWork.Projects.Update(project);
        return project.Progress;
    }
}