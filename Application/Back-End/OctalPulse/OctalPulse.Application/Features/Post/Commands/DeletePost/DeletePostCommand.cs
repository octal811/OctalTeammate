using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Post.Commands.DeletePost;

public record DeletePostCommand(Guid PostId, Guid UserId) : IRequest<bool>;

public class DeletePostCommandHandler : IRequestHandler<DeletePostCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public DeletePostCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<bool> Handle(DeletePostCommand request, CancellationToken cancellationToken)
    {
        var post = await _unitOfWork.Posts.GetByIdAsync(request.PostId, cancellationToken);
        if (post == null)
        {
            throw new NotFoundException("Post not found.");
        }

        var isAuthor = post.AuthorId == request.UserId;
        var isProjectCreator = false;

        if (!isAuthor && post.ProjectId.HasValue)
        {
            var project = await _unitOfWork.Projects.GetByIdAsync(post.ProjectId.Value, cancellationToken);
            isProjectCreator = project != null && project.CreatedByUserId == request.UserId;
        }

        if (!isAuthor && !isProjectCreator)
        {
            throw new ForbiddenException("You do not have permission to delete this post.");
        }

        post.IsDeleted = true;
        post.ModifiedDate = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.PostDeletedAsync(post.Id, post.ProjectId, post.TrackId, cancellationToken);

        return true;
    }
}
