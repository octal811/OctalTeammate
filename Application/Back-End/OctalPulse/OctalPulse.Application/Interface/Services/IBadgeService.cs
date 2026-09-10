using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Interface.Services;

/// <summary>
/// Evaluates and grants badges based on work activity.
/// All badge criteria are cumulative: a badge only ever upgrades to a higher level.
/// </summary>
public interface IBadgeService
{
    /// <summary>
    /// Records newly added work time toward the daily ledger and evaluates the Critical Focus badge.
    /// Call before the surrounding operation is committed so the grant shares the same transaction.
    /// </summary>
    Task EvaluateCriticalFocusAsync(Guid userId, long deltaSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts solved bug tasks (JobType = SolveBug and State = Done) and evaluates the Bug Hunter badge.
    /// <paramref name="justCompletedSolveBug"/> accounts for a task whose state flipped to Done in the
    /// current, not-yet-committed operation.
    /// </summary>
    Task EvaluateBugHunterAsync(Guid userId, bool justCompletedSolveBug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the contribution percentage of each creator of a completed major task and
    /// evaluates the Heavy Work badge for every qualifying contributor.
    /// </summary>
    Task EvaluateHeavyWorkAsync(Guid majorTaskId, CancellationToken cancellationToken = default);
}