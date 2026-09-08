using Microsoft.EntityFrameworkCore;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class PostReactionRepository : BaseRepository<PostReaction>, IPostReactionRepository
{
    public PostReactionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<PostReaction?> GetByUserAndPostAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.PostReactions
            .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId, cancellationToken);
    }
}
