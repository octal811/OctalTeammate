using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Infrastructure.Api;

namespace OctalPulse.Infrastructure.Services;

public class BadgeService : IBadgeService
{
    private readonly ApiClient _apiClient;

    public BadgeService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<GetMyBadgesResponse?> GetMyBadgesAsync(CancellationToken cancellationToken = default)
    {
        return await _apiClient.SendAsync<GetMyBadgesResponse>(
            HttpMethod.Get,
            "/api/badges",
            null,
            cancellationToken);
    }
}