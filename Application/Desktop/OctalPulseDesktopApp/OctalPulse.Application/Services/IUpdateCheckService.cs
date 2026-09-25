using OctalPulse.Application.Contracts;

namespace OctalPulse.Application.Services;

public interface IUpdateCheckService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
    Task<InstallationMetadata?> GetInstallationMetadataAsync();
    string GetCurrentInstallPath();
    bool IsInterruptedInstallation();
}
