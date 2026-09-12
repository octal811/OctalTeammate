using OctalPulse.Application.Contracts;

namespace OctalPulse.Application.Services;

public interface IActivityService
{
    Task<GetMonthlyActivityResponse?> GetMonthlyActivityAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<GetMonthlyActivityResponse?> GetMonthlyActivityAsync(
        Guid userId,
        int year,
        int month,
        CancellationToken cancellationToken = default);
}