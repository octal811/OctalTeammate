using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Interface.Repositories;

public interface IProjectRepository : IGenericRepository<Project>
{
    Task<Project?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
}
