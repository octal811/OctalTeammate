using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Application.Features.Post.Commands.DeletePostComment;

public record DeletePostCommentCommand(
    Guid PostId,
    Guid CommentId,
    Guid UserId = default) : IRequest<bool>;

public class DeletePostCommentCommandHandler : IRequestHandler<DeletePostCommentCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public DeletePostCommentCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<bool> Handle(DeletePostCommentCommand request, CancellationToken cancellationToken)
    {
        var post = await _unitOfWork.Posts.GetByIdAsync(request.PostId, cancellationToken);
        if (post == null)
        {
            throw new NotFoundException("Post not found.");
        }

        var comment = await _unitOfWork.PostComments.GetByIdAsync(request.CommentId, cancellationToken);
        if (comment == null || comment.PostId != request.PostId)
        {
            throw new NotFoundException("Comment not found.");
        }

        var isCommentAuthor = comment.UserId == request.UserId;
        var isPostAuthor = post.AuthorId == request.UserId;

        if (!isCommentAuthor && !isPostAuthor)
        {
            throw new ForbiddenException("You do not have permission to delete this comment.");
        }

        comment.IsDeleted = true;
        comment.ModifiedDate = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.CommentDeletedAsync(post.Id, comment.Id, post.ProjectId, cancellationToken);

        return true;
    }
}
