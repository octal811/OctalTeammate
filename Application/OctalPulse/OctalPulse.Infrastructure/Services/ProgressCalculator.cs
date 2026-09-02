using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Services;

public class ProgressCalculator : IProgressCalculator
{
    private readonly IUnitOfWork _unitOfWork;

    public ProgressCalculator(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<int> GetTrackProgressAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        var track = await _unitOfWork.Tracks.GetWithMajorTasksAsync(trackId, cancellationToken);
        if (track is null)
            return 0;

        return AverageProgress(track.MajorTasks);
    }

    public async Task<int> GetProjectProgressAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var tracks = await _unitOfWork.Tracks.GetByProjectIdsWithMajorTasksAsync(
            new[] { projectId },
            cancellationToken);

        return AverageProgress(tracks);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetProjectsProgressAsync(
        IEnumerable<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        var tracks = await _unitOfWork.Tracks.GetByProjectIdsWithMajorTasksAsync(projectIds, cancellationToken);

        var result = new Dictionary<Guid, int>();
        foreach (var group in tracks.GroupBy(t => t.ProjectId))
            result[group.Key] = AverageProgress(group);

        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetTracksProgressAsync(
        IEnumerable<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        var tracks = await _unitOfWork.Tracks.GetByIdsWithMajorTasksAsync(trackIds, cancellationToken);

        return tracks.ToDictionary(t => t.Id, GetTrackProgress);
    }

    private static int AverageProgress(IEnumerable<MajorTask> majorTasks)
    {
        var list = majorTasks.ToList();
        return list.Count == 0 ? 0 : (int)Math.Round(list.Average(m => m.Progress));
    }

    private static int AverageProgress(IEnumerable<Track> tracks)
    {
        var list = tracks.ToList();
        return list.Count == 0 ? 0 : (int)Math.Round(list.Average(t => GetTrackProgress(t)));
    }

    private static int GetTrackProgress(Track track)
        => track.MajorTasks.Count == 0
            ? 0
            : (int)Math.Round(track.MajorTasks.Average(mt => mt.Progress));
}