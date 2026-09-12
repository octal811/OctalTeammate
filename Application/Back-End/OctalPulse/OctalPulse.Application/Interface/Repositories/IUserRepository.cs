using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Interface.Repositories;

public interface IUserRepository : IGenericRepository<User>
{
    Task<IReadOnlyList<User>> SearchAsync(
        string query,
        int take,
        CancellationToken cancellationToken = default);
}
