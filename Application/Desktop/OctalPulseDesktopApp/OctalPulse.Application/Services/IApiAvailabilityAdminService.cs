using OctalPulse.Application.Contracts;

namespace OctalPulse.Application.Services;

public interface IApiAvailabilityAdminService
{
    Task<AvailabilityResponse> EnableAsync(string key, CancellationToken cancellationToken = default);
    Task<AvailabilityResponse> DisableAsync(string key, CancellationToken cancellationToken = default);
    Task<ApiAvailabilityStatus> GetStatusAsync(string key, CancellationToken cancellationToken = default);
}
