using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Domain.Enums;
using OctalPulse.Infrastructure.Api;

namespace OctalPulse.Infrastructure.Services;

public class PostService : IPostService
{
    private readonly ApiClient _apiClient;

    public PostService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<PostItem> CreatePostAsync(CreatePostClientRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<PostItem>(
            HttpMethod.Post,
            "/api/posts",
            request,
            cancellationToken);
    }

    public Task<PaginatedList<PostItem>> GetFeedPostsAsync(GetFeedPostsClientRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<PaginatedList<PostItem>>(
            HttpMethod.Post,
            "/api/posts/feed",
            request,
            cancellationToken);
    }

    public Task<PaginatedList<PostItem>> GetProfilePostsAsync(Guid userId, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<PaginatedList<PostItem>>(
            HttpMethod.Get,
            $"/api/posts/profile/{userId}?pageNumber={pageNumber}&pageSize={pageSize}",
            null,
            cancellationToken);
    }

    public Task<PostItem> GetPostByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<PostItem>(
            HttpMethod.Get,
            $"/api/posts/{id}",
            null,
            cancellationToken);
    }

    public Task<PostItem> UpdatePostAsync(Guid id, UpdatePostClientRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<PostItem>(
            HttpMethod.Put,
            $"/api/posts/{id}",
            request,
            cancellationToken);
    }

    public Task DeletePostAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync(
            HttpMethod.Delete,
            $"/api/posts/{id}",
            null,
            cancellationToken);
    }

    public Task<PostItem> ToggleReactionAsync(Guid id, ReactionType type, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<PostItem>(
            HttpMethod.Post,
            $"/api/posts/{id}/reactions",
            new ToggleReactionClientRequest(type),
            cancellationToken);
    }

    public Task<CommentItem> AddCommentAsync(Guid id, AddCommentClientRequest request, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<CommentItem>(
            HttpMethod.Post,
            $"/api/posts/{id}/comments",
            request,
            cancellationToken);
    }

    public Task<PaginatedList<CommentItem>> GetCommentsAsync(Guid id, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<PaginatedList<CommentItem>>(
            HttpMethod.Get,
            $"/api/posts/{id}/comments?pageNumber={pageNumber}&pageSize={pageSize}",
            null,
            cancellationToken);
    }

    public Task DeleteCommentAsync(Guid id, Guid commentId, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync(
            HttpMethod.Delete,
            $"/api/posts/{id}/comments/{commentId}",
            null,
            cancellationToken);
    }
}
