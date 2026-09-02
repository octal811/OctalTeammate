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

    public async Task<IReadOnlyList<Track>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await _context.Tracks
            .Where(t => t.ProjectId == projectId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Track?> GetWithMajorTasksAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tracks
            .Include(t => t.MajorTasks)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }
}
