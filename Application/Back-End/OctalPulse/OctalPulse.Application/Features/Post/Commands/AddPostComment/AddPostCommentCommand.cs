using FluentValidation;
using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Features.Post.Models;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Post.Commands.AddPostComment;

public record AddPostCommentCommand(
    Guid PostId,
    string Content,
    Guid? ParentCommentId = null,
    Guid UserId = default) : IRequest<CommentResponse>;

public class AddPostCommentCommandValidator : AbstractValidator<AddPostCommentCommand>
{
    public AddPostCommentCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty();
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Comment content cannot be empty.")
            .MaximumLength(800).WithMessage("Comment and replies cannot exceed 800 characters.");
    }
}

public class AddPostCommentCommandHandler : IRequestHandler<AddPostCommentCommand, CommentResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly IBadgeService _badgeService;

    public AddPostCommentCommandHandler(
        IUnitOfWork unitOfWork,
        IRealtimeNotifier realtimeNotifier,
        IBadgeService badgeService)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
        _badgeService = badgeService;
    }

    public async Task<CommentResponse> Handle(AddPostCommentCommand request, CancellationToken cancellationToken)
    {
        var post = await _unitOfWork.Posts.GetByIdAsync(request.PostId, cancellationToken);
        if (post == null)
        {
            throw new NotFoundException("Post not found.");
        }

        if (post.ProjectId.HasValue)
        {
            var isMember = await _unitOfWork.ProjectMembers.AnyAsync(
                m => m.ProjectId == post.ProjectId.Value && m.UserId == request.UserId && m.Status == MembershipStatus.Approved,
                cancellationToken);

            if (!isMember)
            {
                throw new ForbiddenException("You must be an approved member of this project to comment on this post.");
            }
        }

        if (request.ParentCommentId.HasValue)
        {
            var parent = await _unitOfWork.PostComments.GetByIdAsync(request.ParentCommentId.Value, cancellationToken);
            if (parent == null || parent.PostId != request.PostId || parent.IsDeleted)
            {
                throw new NotFoundException("Parent comment not found on this post.");
            }
        }

        var comment = new PostComment
        {
            Id = Guid.NewGuid(),
            PostId = request.PostId,
            UserId = request.UserId,
            Content = request.Content.Trim(),
            ParentCommentId = request.ParentCommentId,
            CreatedDate = DateTime.UtcNow,
            IsDeleted = false
        };

        await _unitOfWork.PostComments.AddAsync(comment, cancellationToken);
        await _badgeService.EvaluateCommunityVoiceAsync(request.UserId, true, cancellationToken);
        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.CommentAddedAsync(post.Id, comment.Id, comment.ParentCommentId, post.ProjectId, cancellationToken);

        var loadedComment = await _unitOfWork.PostComments.GetWithRepliesAsync(comment.Id, cancellationToken);
        return PostMapper.ToResponse(loadedComment ?? comment);
    }
}
