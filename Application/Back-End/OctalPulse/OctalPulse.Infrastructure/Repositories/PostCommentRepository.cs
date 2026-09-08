using Microsoft.EntityFrameworkCore;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class PostCommentRepository : BaseRepository<PostComment>, IPostCommentRepository
{
    public PostCommentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<List<PostComment>> GetCommentsByPostIdAsync(Guid postId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _context.PostComments
            .AsNoTracking()
            .AsSplitQuery()
            .Where(c => c.PostId == postId && c.ParentCommentId == null)
            .Include(c => c.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.User)
            .OrderBy(c => c.CreatedDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCommentsCountByPostIdAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        return await _context.PostComments
            .AsNoTracking()
            .Where(c => c.PostId == postId && c.ParentCommentId == null)
            .CountAsync(cancellationToken);
    }

    public async Task<PostComment?> GetWithRepliesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PostComments
            .AsSplitQuery()
            .Include(c => c.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }
}
