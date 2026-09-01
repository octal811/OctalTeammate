using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class UserProjectRoleRepository : BaseRepository<UserProjectRole>, IUserProjectRoleRepository
{
    public UserProjectRoleRepository(ApplicationDbContext context) : base(context)
    {
    }
}
