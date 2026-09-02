using Microsoft.EntityFrameworkCore;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class TrackRepository : BaseRepository<Track>, ITrackRepository
{
    public TrackRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Track?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tracks
            .AsNoTracking()
            .Include(t => t.Project)
            .Include(t => t.TrackLeadUser)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Track?> GetByIdWithTreeIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tracks
            .IgnoreQueryFilters()
            .Include(t => t.Members)
            .Include(t => t.MajorTasks)
                .ThenInclude(mt => mt.MinorTasks)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Track?> GetWithMajorTasksAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tracks
            .AsNoTracking()
            .Include(t => t.MajorTasks)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Track>> GetByIdsWithMajorTasksAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return new List<Track>();

        return await _context.Tracks
            .AsNoTracking()
            .Include(t => t.MajorTasks)
            .Where(t => idList.Contains(t.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Track>> GetByProjectIdsWithMajorTasksAsync(
        IEnumerable<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        var ids = projectIds.ToList();
        if (ids.Count == 0)
            return new List<Track>();

        return await _context.Tracks
            .AsNoTracking()
            .Include(t => t.MajorTasks)
            .Where(t => ids.Contains(t.ProjectId))
            .ToListAsync(cancellationToken);
    }
}
