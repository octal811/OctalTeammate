using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Interface.Repositories;

public interface IPostRepository : IGenericRepository<Post>
{
    Task<Post?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<Post>> GetFeedPostsAsync(
        List<Guid> memberProjectIds,
        Guid? specificProjectId,
        Guid? specificTrackId,
        bool onlyGeneral,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> GetFeedPostsCountAsync(
        List<Guid> memberProjectIds,
        Guid? specificProjectId,
        Guid? specificTrackId,
        bool onlyGeneral,
        CancellationToken cancellationToken = default);

    Task<List<Post>> GetProfilePostsAsync(
        Guid authorId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> GetProfilePostsCountAsync(
        Guid authorId,
        CancellationToken cancellationToken = default);
}
