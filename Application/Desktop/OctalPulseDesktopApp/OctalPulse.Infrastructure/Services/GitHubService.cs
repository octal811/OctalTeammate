using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OctalPulse.Application.Services;

namespace OctalPulse.Infrastructure.Services;

public class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubService> _logger;

    public GitHubService(HttpClient httpClient, ILogger<GitHubService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "OctalPulse-Desktop-App");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
    }

    public async Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "user");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var res = await _httpClient.SendAsync(req, cancellationToken);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate GitHub token.");
            return false;
        }
    }

    public async Task<GitHubUserInfo?> GetUserProfileAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "user");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var res = await _httpClient.SendAsync(req, cancellationToken);
            if (!res.IsSuccessStatusCode) return null;

            var content = await res.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            return new GitHubUserInfo(
                root.GetProperty("login").GetString() ?? string.Empty,
                root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty,
                root.GetProperty("avatar_url").GetString() ?? string.Empty,
                root.GetProperty("html_url").GetString() ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get GitHub user profile.");
            return null;
        }
    }

    public async Task<IReadOnlyList<GitHubRepoInfo>> GetUserRepositoriesAsync(string token, CancellationToken cancellationToken = default)
    {
        var list = new List<GitHubRepoInfo>();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "user/repos?sort=updated&per_page=30");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var res = await _httpClient.SendAsync(req, cancellationToken);
            if (!res.IsSuccessStatusCode) return list;

            var content = await res.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var fullName = item.GetProperty("full_name").GetString() ?? string.Empty;
                var description = item.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
                var htmlUrl = item.GetProperty("html_url").GetString() ?? string.Empty;
                var isPrivate = item.GetProperty("private").GetBoolean();

                list.Add(new GitHubRepoInfo(fullName, description ?? string.Empty, htmlUrl, isPrivate));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch user repositories.");
        }
        return list;
    }

    public async Task<GitHubIssueOrPrInfo?> GetIssueOrPrAsync(string repoFullName, int number, string? token = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"repos/{repoFullName}/issues/{number}");
            if (!string.IsNullOrEmpty(token))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            using var res = await _httpClient.SendAsync(req, cancellationToken);
            if (!res.IsSuccessStatusCode) return null;

            var content = await res.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var isPr = root.TryGetProperty("pull_request", out _);
            return new GitHubIssueOrPrInfo(
                root.GetProperty("number").GetInt32(),
                root.GetProperty("title").GetString() ?? string.Empty,
                root.GetProperty("state").GetString() ?? string.Empty,
                root.GetProperty("html_url").GetString() ?? string.Empty,
                isPr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get issue or PR #{Number} from {Repo}", number, repoFullName);
            return null;
        }
    }
}
