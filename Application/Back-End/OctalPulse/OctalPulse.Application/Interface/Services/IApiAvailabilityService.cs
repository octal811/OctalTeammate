namespace OctalPulse.Application.Interface.Services;

public record ApiAvailabilityStatus(
    string Key,
    bool IsEnabled,
    DateTime UpdatedAt,
    Guid? UpdatedBy);

public interface IApiAvailabilityService
{
    Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default);
    Task EnableAsync(string key, Guid? updatedBy, CancellationToken cancellationToken = default);
    Task DisableAsync(string key, Guid? updatedBy, CancellationToken cancellationToken = default);
    Task<ApiAvailabilityStatus> GetStatusAsync(string key, CancellationToken cancellationToken = default);
}