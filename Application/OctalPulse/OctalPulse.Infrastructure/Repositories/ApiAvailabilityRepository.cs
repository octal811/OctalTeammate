using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class ApiAvailabilityRepository : BaseRepository<ApiAvailability>, IApiAvailabilityRepository
{
    public ApiAvailabilityRepository(ApplicationDbContext context) : base(context)
    {
    }
}