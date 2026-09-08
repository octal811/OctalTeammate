using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Features.Post.Models;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Post.Queries.GetFeedPosts;

public record GetFeedPostsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    Guid? ProjectId = null,
    Guid? TrackId = null,
    bool OnlyGeneral = false,
    Guid UserId = default) : IRequest<PaginatedListResponse<PostResponse>>;

public class GetFeedPostsQueryHandler : IRequestHandler<GetFeedPostsQuery, PaginatedListResponse<PostResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetFeedPostsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PaginatedListResponse<PostResponse>> Handle(GetFeedPostsQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        var memberProjectMembers = await _unitOfWork.ProjectMembers.FindAsync(
            m => m.UserId == request.UserId && m.Status == MembershipStatus.Approved,
            cancellationToken);

        var memberProjectIds = memberProjectMembers.Select(m => m.ProjectId).ToList();

        if (request.ProjectId.HasValue)
        {
            if (!memberProjectIds.Contains(request.ProjectId.Value))
            {
                throw new ForbiddenException("You must be an approved member of this project to view its posts.");
            }
        }

        var posts = await _unitOfWork.Posts.GetFeedPostsAsync(
            memberProjectIds,
            request.ProjectId,
            request.TrackId,
            request.OnlyGeneral,
            pageNumber,
            pageSize,
            cancellationToken);

        var totalCount = await _unitOfWork.Posts.GetFeedPostsCountAsync(
            memberProjectIds,
            request.ProjectId,
            request.TrackId,
            request.OnlyGeneral,
            cancellationToken);

        var items = posts.Select(p => PostMapper.ToResponse(p, request.UserId)).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedListResponse<PostResponse>(items, totalCount, pageNumber, pageSize, totalPages);
    }
}
