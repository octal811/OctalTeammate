namespace OctalPulse.Application.Services;

public record GitHubUserInfo(string Login, string Name, string AvatarUrl, string HtmlUrl);
public record GitHubRepoInfo(string FullName, string Description, string HtmlUrl, bool IsPrivate);
public record GitHubIssueOrPrInfo(int Number, string Title, string State, string HtmlUrl, bool IsPullRequest);

public interface IGitHubService
{
    Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<GitHubUserInfo?> GetUserProfileAsync(string token, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitHubRepoInfo>> GetUserRepositoriesAsync(string token, CancellationToken cancellationToken = default);
    Task<GitHubIssueOrPrInfo?> GetIssueOrPrAsync(string repoFullName, int number, string? token = null, CancellationToken cancellationToken = default);
}
