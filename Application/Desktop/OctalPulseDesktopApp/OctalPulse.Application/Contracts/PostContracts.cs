using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Contracts;

public record PostItem(
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

public record CommentItem(
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
    List<CommentItem> Replies);

public record PaginatedList<T>(
    List<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);

public record CreatePostClientRequest(
    string Content,
    string? PhotoUrl = null,
    Guid? ProjectId = null,
    Guid? TrackId = null);

public record GetFeedPostsClientRequest(
    int PageNumber = 1,
    int PageSize = 10,
    Guid? ProjectId = null,
    Guid? TrackId = null,
    bool OnlyGeneral = false);

public record UpdatePostClientRequest(
    string Content,
    string? PhotoUrl = null);

public record ToggleReactionClientRequest(
    ReactionType Type);

public record AddCommentClientRequest(
    string Content,
    Guid? ParentCommentId = null);
