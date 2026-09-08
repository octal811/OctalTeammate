using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Interface.Repositories;

public interface IPostReactionRepository : IGenericRepository<PostReaction>
{
    Task<PostReaction?> GetByUserAndPostAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default);
}
