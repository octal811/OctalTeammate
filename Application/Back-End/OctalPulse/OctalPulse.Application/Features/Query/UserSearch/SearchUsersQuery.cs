using MediatR;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.UserSearch;

public record SearchUsersQuery(string Query) : IRequest<SearchUsersResponse>;

public record SearchUsersResponse(IReadOnlyList<UserSearchResult> Results);

public record UserSearchResult(
    Guid Id,
    string Name,
    string Email,
    UserRole MainRole,
    UserRank Rank,
    string? ProfilePictureUrl);