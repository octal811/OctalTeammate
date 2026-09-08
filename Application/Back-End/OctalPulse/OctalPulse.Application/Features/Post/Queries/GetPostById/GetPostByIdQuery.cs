using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Features.Post.Models;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Post.Queries.GetPostById;

public record GetPostByIdQuery(Guid PostId, Guid UserId = default) : IRequest<PostResponse>;

public class GetPostByIdQueryHandler : IRequestHandler<GetPostByIdQuery, PostResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPostByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PostResponse> Handle(GetPostByIdQuery request, CancellationToken cancellationToken)
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
                throw new ForbiddenException("You must be an approved member of this project to view this post.");
            }
        }

        return PostMapper.ToResponse(post, request.UserId);
    }
}
