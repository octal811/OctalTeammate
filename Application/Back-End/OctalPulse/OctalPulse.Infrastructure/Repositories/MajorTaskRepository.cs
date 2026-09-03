using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class MajorTaskRepository : BaseRepository<MajorTask>, IMajorTaskRepository
{
    public MajorTaskRepository(ApplicationDbContext context) : base(context)
    {
    }
}
