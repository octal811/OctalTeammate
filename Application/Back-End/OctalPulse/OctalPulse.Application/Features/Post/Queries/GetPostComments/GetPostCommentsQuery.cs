using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Features.Post.Models;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Post.Queries.GetPostComments;

public record GetPostCommentsQuery(
    Guid PostId,
    int PageNumber = 1,
    int PageSize = 10,
    Guid UserId = default) : IRequest<PaginatedListResponse<CommentResponse>>;

public class GetPostCommentsQueryHandler : IRequestHandler<GetPostCommentsQuery, PaginatedListResponse<CommentResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPostCommentsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PaginatedListResponse<CommentResponse>> Handle(GetPostCommentsQuery request, CancellationToken cancellationToken)
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
                throw new ForbiddenException("You must be an approved member of this project to view comments.");
            }
        }

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        var comments = await _unitOfWork.PostComments.GetCommentsByPostIdAsync(request.PostId, pageNumber, pageSize, cancellationToken);
        var totalCount = await _unitOfWork.PostComments.GetCommentsCountByPostIdAsync(request.PostId, cancellationToken);

        var items = comments.Select(PostMapper.ToResponse).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedListResponse<CommentResponse>(items, totalCount, pageNumber, pageSize, totalPages);
    }
}
