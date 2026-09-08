using MediatR;
using OctalPulse.Application.Features.Post.Models;
using OctalPulse.Application.Interface.Repositories;

namespace OctalPulse.Application.Features.Post.Queries.GetProfilePosts;

public record GetProfilePostsQuery(
    Guid ProfileUserId,
    int PageNumber = 1,
    int PageSize = 10,
    Guid ViewerUserId = default) : IRequest<PaginatedListResponse<PostResponse>>;

public class GetProfilePostsQueryHandler : IRequestHandler<GetProfilePostsQuery, PaginatedListResponse<PostResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetProfilePostsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PaginatedListResponse<PostResponse>> Handle(GetProfilePostsQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        var posts = await _unitOfWork.Posts.GetProfilePostsAsync(
            request.ProfileUserId,
            pageNumber,
            pageSize,
            cancellationToken);

        var totalCount = await _unitOfWork.Posts.GetProfilePostsCountAsync(
            request.ProfileUserId,
            cancellationToken);

        var items = posts.Select(p => PostMapper.ToResponse(p, request.ViewerUserId)).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedListResponse<PostResponse>(items, totalCount, pageNumber, pageSize, totalPages);
    }
}
