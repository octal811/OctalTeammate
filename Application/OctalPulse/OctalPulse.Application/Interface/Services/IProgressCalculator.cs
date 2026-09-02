using OctalPulse.Application.Interface.Repositories;

namespace OctalPulse.Application.Interface.Services;

public interface IProgressCalculator
{
    Task<int> RecalculateTrackProgressAsync(Guid trackId, IUnitOfWork unitOfWork, CancellationToken cancellationToken = default);
    Task<int> RecalculateProjectProgressAsync(Guid projectId, IUnitOfWork unitOfWork, CancellationToken cancellationToken = default);
}