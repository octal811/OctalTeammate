using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Infrastructure.Api;

namespace OctalPulse.Infrastructure.Services;

public class ActivityService : IActivityService
{
    private readonly ApiClient _apiClient;

    public ActivityService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<GetMonthlyActivityResponse?> GetMonthlyActivityAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        return await _apiClient.SendAsync<GetMonthlyActivityResponse>(
            HttpMethod.Get,
            $"/api/activity/monthly/{year}/{month}",
            null,
            cancellationToken);
    }
}