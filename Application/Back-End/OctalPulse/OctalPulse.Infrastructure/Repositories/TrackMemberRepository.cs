using Microsoft.EntityFrameworkCore;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class TrackMemberRepository : BaseRepository<TrackMember>, ITrackMemberRepository
{
    public TrackMemberRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<TrackMember?> GetIncludingDeletedAsync(
        Guid trackId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TrackMembers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                m => m.TrackId == trackId && m.UserId == userId,
                cancellationToken);
    }
}
