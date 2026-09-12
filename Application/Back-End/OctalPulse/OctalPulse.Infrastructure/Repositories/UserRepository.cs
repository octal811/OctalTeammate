using Microsoft.EntityFrameworkCore;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<User>> SearchAsync(
        string query,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .Where(u => u.Name.Contains(query) || (u.Email != null && u.Email.Contains(query)))
            .OrderBy(u => u.Name)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}