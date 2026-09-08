using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Post.Models;

public record PostResponse(
    Guid Id,
    Guid AuthorId,
    string AuthorName,
    UserRole AuthorRole,
    string? AuthorAvatarUrl,
    string Content,
    string? PhotoUrl,
    Guid? ProjectId,
    string? ProjectTitle,
    Guid? TrackId,
    string? TrackName,
    DateTime CreatedDate,
    DateTime? ModifiedDate,
    int LikeCount,
    int LoveCount,
    int CelebrateCount,
    int InsightfulCount,
    int TotalReactions,
    ReactionType? UserReaction,
    int CommentsCount);

public record CommentResponse(
    Guid Id,
    Guid PostId,
    Guid UserId,
    string UserName,
    UserRole UserRole,
    string? UserAvatarUrl,
    string Content,
    Guid? ParentCommentId,
    DateTime CreatedDate,
    DateTime? ModifiedDate,
    int RepliesCount,
    List<CommentResponse> Replies);

public record PaginatedListResponse<T>(
    List<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);
