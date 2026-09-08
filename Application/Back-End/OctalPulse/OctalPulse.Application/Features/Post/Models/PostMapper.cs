using OctalPulse.Application.Features.Post.Models;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Post.Models;

public static class PostMapper
{
    public static PostResponse ToResponse(Domain.Entities.Post post, Guid currentUserId)
    {
        var reactions = post.Reactions ?? new List<PostReaction>();
        var userReaction = reactions.FirstOrDefault(r => r.UserId == currentUserId)?.Type;

        var likeCount = reactions.Count(r => r.Type == ReactionType.Like);
        var loveCount = reactions.Count(r => r.Type == ReactionType.Love);
        var celebrateCount = reactions.Count(r => r.Type == ReactionType.Celebrate);
        var insightfulCount = reactions.Count(r => r.Type == ReactionType.Insightful);

        var commentsCount = post.Comments?.Count(c => !c.IsDeleted) ?? 0;

        return new PostResponse(
            post.Id,
            post.AuthorId,
            post.Author?.Name ?? string.Empty,
            post.Author?.MainRole ?? UserRole.SoftwareEngineer,
            post.Author?.ProfilePictureUrl,
            post.Content,
            post.PhotoUrl,
            post.ProjectId,
            post.Project?.Title,
            post.TrackId,
            post.Track?.Name,
            post.CreatedDate,
            post.ModifiedDate,
            likeCount,
            loveCount,
            celebrateCount,
            insightfulCount,
            reactions.Count,
            userReaction,
            commentsCount);
    }

    public static CommentResponse ToResponse(PostComment comment)
    {
        var replies = comment.Replies?
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.CreatedDate)
            .Select(ToResponse)
            .ToList() ?? new List<CommentResponse>();

        return new CommentResponse(
            comment.Id,
            comment.PostId,
            comment.UserId,
            comment.User?.Name ?? string.Empty,
            comment.User?.MainRole ?? UserRole.SoftwareEngineer,
            comment.User?.ProfilePictureUrl,
            comment.Content,
            comment.ParentCommentId,
            comment.CreatedDate,
            comment.ModifiedDate,
            replies.Count,
            replies);
    }
}
