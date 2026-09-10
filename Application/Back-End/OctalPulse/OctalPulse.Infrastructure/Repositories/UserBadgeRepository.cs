using Microsoft.EntityFrameworkCore;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class UserBadgeRepository : BaseRepository<UserBadge>, IUserBadgeRepository
{
    public UserBadgeRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<UserBadge?> GetByTypeAsync(
        Guid userId,
        Domain.Enums.BadgeType type,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Type == type, cancellationToken);
    }
}