using FluentValidation;
using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Features.Post.Models;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Application.Features.Post.Commands.UpdatePost;

public record UpdatePostCommand(
    Guid PostId,
    string Content,
    string? PhotoUrl = null,
    Guid UserId = default) : IRequest<PostResponse>;

public class UpdatePostCommandValidator : AbstractValidator<UpdatePostCommand>
{
    public UpdatePostCommandValidator()
    {
        RuleFor(x => x.PostId).NotEmpty();
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Post content cannot be empty.")
            .MaximumLength(1800).WithMessage("Post content cannot exceed 1800 characters.");
        RuleFor(x => x.PhotoUrl)
            .MaximumLength(2048).WithMessage("Photo link cannot exceed 2048 characters.");
    }
}

public class UpdatePostCommandHandler : IRequestHandler<UpdatePostCommand, PostResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public UpdatePostCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<PostResponse> Handle(UpdatePostCommand request, CancellationToken cancellationToken)
    {
        var post = await _unitOfWork.Posts.GetWithDetailsAsync(request.PostId, cancellationToken);
        if (post == null)
        {
            throw new NotFoundException("Post not found.");
        }

        if (post.AuthorId != request.UserId)
        {
            throw new ForbiddenException("Only the author can update this post.");
        }

        post.Content = request.Content.Trim();
        post.PhotoUrl = string.IsNullOrWhiteSpace(request.PhotoUrl) ? null : request.PhotoUrl.Trim();
        post.ModifiedDate = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.PostUpdatedAsync(post.Id, post.ProjectId, post.TrackId, cancellationToken);

        return PostMapper.ToResponse(post, request.UserId);
    }
}
