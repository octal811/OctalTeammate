using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Interface.Repositories;

public interface IPostCommentRepository : IGenericRepository<PostComment>
{
    Task<List<PostComment>> GetCommentsByPostIdAsync(Guid postId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetCommentsCountByPostIdAsync(Guid postId, CancellationToken cancellationToken = default);
    Task<PostComment?> GetWithRepliesAsync(Guid id, CancellationToken cancellationToken = default);
}
