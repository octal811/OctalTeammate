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

/// <summary>
/// Evaluates the Work Titan badge from the user's total lifetime work time.
/// </summary>
Task EvaluateWorkTitanAsync(Guid userId, CancellationToken cancellationToken = default);

/// <summary>
/// Evaluates the Streak Master badge from the user's longest consecutive days with logged work.
/// </summary>
Task EvaluateStreakMasterAsync(Guid userId, CancellationToken cancellationToken = default);

/// <summary>
/// Evaluates the Task Finisher badge from the count of completed minor tasks the user created.
/// <paramref name="justCompleted"/> accounts for a task whose state flipped to Done in the
/// current, not-yet-committed operation.
/// </summary>
Task EvaluateTaskFinisherAsync(Guid userId, bool justCompleted, CancellationToken cancellationToken = default);

/// <summary>
/// Evaluates the All Rounder badge from the number of distinct job types with at least one
/// completed task. <paramref name="justCompletedJobType"/> accounts for a task that flipped to
/// Done in the current, not-yet-committed operation.
/// </summary>
Task EvaluateAllRounderAsync(Guid userId, MinorTaskJobType? justCompletedJobType, CancellationToken cancellationToken = default);

/// <summary>
/// Evaluates the Community Voice badge from the combined count of posts and comments the user authored.
/// <paramref name="justAdded"/> accounts for a post/comment created in the current, not-yet-committed operation.
/// </summary>
Task EvaluateCommunityVoiceAsync(Guid userId, bool justAdded, CancellationToken cancellationToken = default);

/// <summary>
/// Evaluates the Team Captain badge from the number of tracks the user currently leads.
/// <paramref name="justCreated"/> accounts for a track created in the current, not-yet-committed operation.
/// </summary>
Task EvaluateTeamCaptainAsync(Guid userId, bool justCreated, CancellationToken cancellationToken = default);

/// <summary>
/// Evaluates the Team Organizer badge from the most major tasks the user created within a single track.
/// <paramref name="trackId"/> is the track that just received a new major task in the current,
/// not-yet-committed operation.
/// </summary>
Task EvaluateTeamOrganizerAsync(Guid userId, Guid trackId, CancellationToken cancellationToken = default);
}