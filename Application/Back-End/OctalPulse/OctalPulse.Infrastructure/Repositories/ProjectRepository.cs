using Microsoft.EntityFrameworkCore;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class ProjectRepository : BaseRepository<Project>, IProjectRepository
{
    public ProjectRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Project?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .AsNoTracking()
            .Include(p => p.CreatedByUser)
            .Include(p => p.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Project?> GetByIdWithTreeIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Members)
                .ThenInclude(m => m.Roles)
            .Include(p => p.Tracks)
                .ThenInclude(t => t.Members)
            .Include(p => p.Tracks)
                .ThenInclude(t => t.MajorTasks)
                .ThenInclude(mt => mt.MinorTasks)
            .Include(p => p.Events)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }
}
