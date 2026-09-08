using OctalPulse.Application.Contracts;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Services;

public interface IPostService
{
    Task<PostItem> CreatePostAsync(CreatePostClientRequest request, CancellationToken cancellationToken = default);
    Task<PaginatedList<PostItem>> GetFeedPostsAsync(GetFeedPostsClientRequest request, CancellationToken cancellationToken = default);
    Task<PaginatedList<PostItem>> GetProfilePostsAsync(Guid userId, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<PostItem> GetPostByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PostItem> UpdatePostAsync(Guid id, UpdatePostClientRequest request, CancellationToken cancellationToken = default);
    Task DeletePostAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PostItem> ToggleReactionAsync(Guid id, ReactionType type, CancellationToken cancellationToken = default);
    Task<CommentItem> AddCommentAsync(Guid id, AddCommentClientRequest request, CancellationToken cancellationToken = default);
    Task<PaginatedList<CommentItem>> GetCommentsAsync(Guid id, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task DeleteCommentAsync(Guid id, Guid commentId, CancellationToken cancellationToken = default);
}
