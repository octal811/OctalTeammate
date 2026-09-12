using FluentValidation;
using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Features.Post.Models;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Post.Commands.CreatePost;

public record CreatePostCommand(
    string Content,
    string? PhotoUrl = null,
    Guid? ProjectId = null,
    Guid? TrackId = null,
    Guid AuthorId = default) : IRequest<PostResponse>;

public class CreatePostCommandValidator : AbstractValidator<CreatePostCommand>
{
    public CreatePostCommandValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Post content cannot be empty.")
            .MaximumLength(1800).WithMessage("Post content cannot exceed 1800 characters.");

        RuleFor(x => x.PhotoUrl)
            .MaximumLength(2048).WithMessage("Photo link cannot exceed 2048 characters.");
    }
}

public class CreatePostCommandHandler : IRequestHandler<CreatePostCommand, PostResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly IBadgeService _badgeService;

    public CreatePostCommandHandler(
        IUnitOfWork unitOfWork,
        IRealtimeNotifier realtimeNotifier,
        IBadgeService badgeService)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
        _badgeService = badgeService;
    }

    public async Task<PostResponse> Handle(CreatePostCommand request, CancellationToken cancellationToken)
    {
        if (request.ProjectId.HasValue)
        {
            var isMember = await _unitOfWork.ProjectMembers.AnyAsync(
                m => m.ProjectId == request.ProjectId.Value && m.UserId == request.AuthorId && m.Status == MembershipStatus.Approved,
                cancellationToken);

            if (!isMember)
            {
                throw new ForbiddenException("You must be an approved member of this project to create project posts.");
            }

            if (request.TrackId.HasValue)
            {
                var track = await _unitOfWork.Tracks.GetByIdAsync(request.TrackId.Value, cancellationToken);
                if (track == null || track.ProjectId != request.ProjectId.Value)
                {
                    throw new NotFoundException("Track not found within the selected project.");
                }
            }
        }

        var post = new Domain.Entities.Post
        {
            Id = Guid.NewGuid(),
            AuthorId = request.AuthorId,
            Content = request.Content.Trim(),
            PhotoUrl = string.IsNullOrWhiteSpace(request.PhotoUrl) ? null : request.PhotoUrl.Trim(),
            ProjectId = request.ProjectId,
            TrackId = request.TrackId,
            CreatedDate = DateTime.UtcNow,
            IsDeleted = false
        };

        await _unitOfWork.Posts.AddAsync(post, cancellationToken);
        await _badgeService.EvaluateCommunityVoiceAsync(request.AuthorId, true, cancellationToken);
        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.PostCreatedAsync(post.Id, post.ProjectId, post.TrackId, cancellationToken);

        var created = await _unitOfWork.Posts.GetWithDetailsAsync(post.Id, cancellationToken);
        return PostMapper.ToResponse(created ?? post, request.AuthorId);
    }
}
