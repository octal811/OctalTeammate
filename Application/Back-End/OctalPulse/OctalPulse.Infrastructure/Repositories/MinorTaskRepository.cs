using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class MinorTaskRepository : BaseRepository<MinorTask>, IMinorTaskRepository
{
    public MinorTaskRepository(ApplicationDbContext context) : base(context)
    {
    }
}
