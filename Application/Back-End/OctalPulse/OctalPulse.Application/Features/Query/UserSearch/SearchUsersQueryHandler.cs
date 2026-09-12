using MediatR;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Features.Query.UserSearch;

public class SearchUsersQueryHandler : IRequestHandler<SearchUsersQuery, SearchUsersResponse>
{
    private const int MaxResults = 20;

    private readonly IUnitOfWork _unitOfWork;

    public SearchUsersQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SearchUsersResponse> Handle(SearchUsersQuery request, CancellationToken cancellationToken)
    {
        var query = request.Query.Trim();
        if (string.IsNullOrWhiteSpace(query))
            return new SearchUsersResponse(Array.Empty<UserSearchResult>());

        var users = await _unitOfWork.Users.SearchAsync(query, MaxResults, cancellationToken);

        var results = users
            .Select(u => new UserSearchResult(u.Id, u.Name, u.Email ?? string.Empty, u.MainRole, u.Rank, u.ProfilePictureUrl))
            .ToList();

        return new SearchUsersResponse(results);
    }
}