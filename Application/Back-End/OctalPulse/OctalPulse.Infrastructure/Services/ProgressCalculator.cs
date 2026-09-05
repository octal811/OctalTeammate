using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

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

        return PercentDone(track.MajorTasks);
    }

    public async Task<int> GetProjectProgressAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var tracks = await _unitOfWork.Tracks.GetByProjectIdsWithMajorTasksAsync(
            new[] { projectId },
            cancellationToken);

        return PercentDone(tracks.SelectMany(t => t.MajorTasks));
    }

    public async Task<int> GetMajorTaskProgressAsync(Guid majorTaskId, CancellationToken cancellationToken = default)
    {
        var minorTasks = (await _unitOfWork.MinorTasks.FindAsync(
            mn => mn.MajorTaskId == majorTaskId,
            cancellationToken)).ToList();

        return PercentDone(minorTasks.Select(mn => mn.State == MinorTaskState.Done));
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetProjectsProgressAsync(
        IEnumerable<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        var tracks = await _unitOfWork.Tracks.GetByProjectIdsWithMajorTasksAsync(projectIds, cancellationToken);

        var result = new Dictionary<Guid, int>();
        foreach (var group in tracks.GroupBy(t => t.ProjectId))
            result[group.Key] = PercentDone(group.SelectMany(t => t.MajorTasks));

        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetTracksProgressAsync(
        IEnumerable<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        var tracks = await _unitOfWork.Tracks.GetByIdsWithMajorTasksAsync(trackIds, cancellationToken);

        return tracks.ToDictionary(t => t.Id, t => PercentDone(t.MajorTasks));
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetMajorTasksProgressAsync(
        IEnumerable<Guid> majorTaskIds,
        CancellationToken cancellationToken = default)
    {
        var ids = majorTaskIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, int>();

        var minorTasks = (await _unitOfWork.MinorTasks.FindAsync(
            mn => ids.Contains(mn.MajorTaskId),
            cancellationToken)).ToList();

        var result = new Dictionary<Guid, int>();
        foreach (var group in minorTasks.GroupBy(mn => mn.MajorTaskId))
            result[group.Key] = PercentDone(group.Select(mn => mn.State == MinorTaskState.Done));

        return result;
    }

    private static int PercentDone(IEnumerable<MajorTask> majorTasks)
    {
        var list = majorTasks.ToList();
        return PercentDone(list.Count(mt => mt.State == MajorTaskState.Done), list.Count);
    }

    private static int PercentDone(IEnumerable<bool> isDone)
    {
        var list = isDone.ToList();
        return PercentDone(list.Count(d => d), list.Count);
    }

    private static int PercentDone(int done, int total)
        => total == 0 ? 0 : (int)Math.Round(100.0 * done / total);
}