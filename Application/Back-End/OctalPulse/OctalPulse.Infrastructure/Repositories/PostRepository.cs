using Microsoft.EntityFrameworkCore;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class PostRepository : BaseRepository<Post>, IPostRepository
{
    public PostRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Post?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Posts
            .AsSplitQuery()
            .Include(p => p.Author)
            .Include(p => p.Project)
            .Include(p => p.Track)
            .Include(p => p.Reactions)
                .ThenInclude(r => r.User)
            .Include(p => p.Comments)
                .ThenInclude(c => c.User)
            .Include(p => p.Comments)
                .ThenInclude(c => c.Replies)
                    .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<List<Post>> GetFeedPostsAsync(
        List<Guid> memberProjectIds,
        Guid? specificProjectId,
        Guid? specificTrackId,
        bool onlyGeneral,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Posts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Author)
            .Include(p => p.Project)
            .Include(p => p.Track)
            .Include(p => p.Reactions)
            .Include(p => p.Comments)
            .AsQueryable();

        if (onlyGeneral)
        {
            query = query.Where(p => p.ProjectId == null);
        }
        else if (specificProjectId.HasValue)
        {
            query = query.Where(p => p.ProjectId == specificProjectId.Value);
            if (specificTrackId.HasValue)
            {
                query = query.Where(p => p.TrackId == specificTrackId.Value);
            }
        }
        else
        {
            // Feed for all user's projects + general posts
            query = query.Where(p => p.ProjectId == null || memberProjectIds.Contains(p.ProjectId.Value));
        }

        return await query
            .OrderByDescending(p => p.CreatedDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetFeedPostsCountAsync(
        List<Guid> memberProjectIds,
        Guid? specificProjectId,
        Guid? specificTrackId,
        bool onlyGeneral,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Posts.AsNoTracking().AsQueryable();

        if (onlyGeneral)
        {
            query = query.Where(p => p.ProjectId == null);
        }
        else if (specificProjectId.HasValue)
        {
            query = query.Where(p => p.ProjectId == specificProjectId.Value);
            if (specificTrackId.HasValue)
            {
                query = query.Where(p => p.TrackId == specificTrackId.Value);
            }
        }
        else
        {
            query = query.Where(p => p.ProjectId == null || memberProjectIds.Contains(p.ProjectId.Value));
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task<List<Post>> GetProfilePostsAsync(
        Guid authorId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await _context.Posts
            .AsNoTracking()
            .AsSplitQuery()
            .Where(p => p.AuthorId == authorId && p.ProjectId == null)
            .Include(p => p.Author)
            .Include(p => p.Reactions)
            .Include(p => p.Comments)
            .OrderByDescending(p => p.CreatedDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetProfilePostsCountAsync(
        Guid authorId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Posts
            .AsNoTracking()
            .Where(p => p.AuthorId == authorId && p.ProjectId == null)
            .CountAsync(cancellationToken);
    }
}
