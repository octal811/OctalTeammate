using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Infrastructure.Api;

namespace OctalPulse.Infrastructure.Services;

public class ApiAvailabilityAdminService : IApiAvailabilityAdminService
{
    private readonly ApiClient _apiClient;

    public ApiAvailabilityAdminService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<AvailabilityResponse> EnableAsync(string key, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<AvailabilityResponse>(
            HttpMethod.Post,
            "/api/admin/ApiAvailability/enable",
            new AvailabilityRequest(key),
            cancellationToken);
    }

    public Task<AvailabilityResponse> DisableAsync(string key, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<AvailabilityResponse>(
            HttpMethod.Post,
            "/api/admin/ApiAvailability/disable",
            new AvailabilityRequest(key),
            cancellationToken);
    }

    public Task<ApiAvailabilityStatus> GetStatusAsync(string key, CancellationToken cancellationToken = default)
    {
        return _apiClient.SendAsync<ApiAvailabilityStatus>(
            HttpMethod.Get,
            "/api/admin/ApiAvailability/status",
            new AvailabilityRequest(key),
            cancellationToken);
    }
}
