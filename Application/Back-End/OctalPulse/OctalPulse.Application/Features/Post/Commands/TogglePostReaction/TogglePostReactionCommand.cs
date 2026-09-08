using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Features.Post.Models;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Post.Commands.TogglePostReaction;

public record TogglePostReactionCommand(
    Guid PostId,
    ReactionType Type,
    Guid UserId = default) : IRequest<PostResponse>;

public class TogglePostReactionCommandHandler : IRequestHandler<TogglePostReactionCommand, PostResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public TogglePostReactionCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<PostResponse> Handle(TogglePostReactionCommand request, CancellationToken cancellationToken)
    {
        var post = await _unitOfWork.Posts.GetWithDetailsAsync(request.PostId, cancellationToken);
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
                throw new ForbiddenException("You must be an approved member of this project to react to this post.");
            }
        }

        var existingReaction = await _unitOfWork.PostReactions.GetByUserAndPostAsync(request.PostId, request.UserId, cancellationToken);

        if (existingReaction != null)
        {
            if (existingReaction.Type == request.Type)
            {
                _unitOfWork.PostReactions.Remove(existingReaction);
                post.Reactions.Remove(existingReaction);
            }
            else
            {
                existingReaction.Type = request.Type;
            }
        }
        else
        {
            var newReaction = new PostReaction
            {
                Id = Guid.NewGuid(),
                PostId = request.PostId,
                UserId = request.UserId,
                Type = request.Type,
                CreatedDate = DateTime.UtcNow
            };
            await _unitOfWork.PostReactions.AddAsync(newReaction, cancellationToken);
            post.Reactions.Add(newReaction);
        }

        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.PostReactionChangedAsync(post.Id, post.ProjectId, cancellationToken);

        return PostMapper.ToResponse(post, request.UserId);
    }
}
