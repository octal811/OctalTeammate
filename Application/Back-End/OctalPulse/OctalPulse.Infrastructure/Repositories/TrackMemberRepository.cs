using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class TrackMemberRepository : BaseRepository<TrackMember>, ITrackMemberRepository
{
    public TrackMemberRepository(ApplicationDbContext context) : base(context)
    {
    }
}
