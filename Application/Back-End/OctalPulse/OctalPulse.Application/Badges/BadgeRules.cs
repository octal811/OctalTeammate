using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Badges;

/// <summary>
/// Static thresholds that map raw metrics to the highest achievable badge level.
/// Levels only climb (a badge is permanent once earned).
/// </summary>
public static class BadgeRules
{
    // Critical Focus — best single-day total work time (in seconds)
    public static IReadOnlyDictionary<BadgeLevel, long> CriticalFocusThresholdSeconds { get; } =
        new Dictionary<BadgeLevel, long>
        {
            [BadgeLevel.Bronze] = 30 * 60,          // 30 minutes
            [BadgeLevel.Silver] = 60 * 60,          // 1 hour
            [BadgeLevel.Gold] = 2 * 60 * 60,        // 2 hours
            [BadgeLevel.Platinum] = 3 * 60 * 60,    // 3 hours
            [BadgeLevel.Diamond] = 4 * 60 * 60,     // 4 hours
            [BadgeLevel.Legend] = 6 * 60 * 60       // 6 hours
        };

    // Heavy Work — share of minor tasks you completed under a finished major task (percentage)
    public static IReadOnlyDictionary<BadgeLevel, int> HeavyWorkThresholdPercent { get; } =
        new Dictionary<BadgeLevel, int>
        {
            [BadgeLevel.Bronze] = 10,
            [BadgeLevel.Silver] = 25,
            [BadgeLevel.Gold] = 50,
            [BadgeLevel.Platinum] = 75,
            [BadgeLevel.Diamond] = 90,
            [BadgeLevel.Legend] = 100
        };

    // Bug Hunter — solved tasks with JobType = SolveBug
    public static IReadOnlyDictionary<BadgeLevel, int> BugHunterThresholdCount { get; } =
        new Dictionary<BadgeLevel, int>
        {
            [BadgeLevel.Bronze] = 3,
            [BadgeLevel.Silver] = 6,
            [BadgeLevel.Gold] = 10,
            [BadgeLevel.Platinum] = 16,
            [BadgeLevel.Diamond] = 25,
            [BadgeLevel.Legend] = 32
        };

    public static BadgeLevel? CriticalFocusLevelFor(long seconds)
        => HighestLevelWhere(CriticalFocusThresholdSeconds, seconds);

    public static BadgeLevel? HeavyWorkLevelFor(int percent)
        => HighestLevelWhere(HeavyWorkThresholdPercent, percent);

    public static BadgeLevel? BugHunterLevelFor(int count)
        => HighestLevelWhere(BugHunterThresholdCount, count);

    /// <summary>
    /// Returns the threshold value (or infinity) just above <paramref name="current"/> for a badge,
    /// so clients can report progress toward the next level.
    /// </summary>
    public static long NextCriticalFocusTarget(long currentSeconds)
        => NextTarget(CriticalFocusThresholdSeconds, currentSeconds);

    public static long NextHeavyWorkTarget(int currentPercent)
        => NextTarget(HeavyWorkThresholdPercent, currentPercent);

    public static long NextBugHunterTarget(int currentCount)
        => NextTarget(BugHunterThresholdCount, currentCount);

    private static BadgeLevel? HighestLevelWhere<T>(IReadOnlyDictionary<BadgeLevel, T> thresholds, T value)
        where T : IComparable<T>
    {
        BadgeLevel? highest = null;
        foreach (var pair in thresholds)
        {
            if (value.CompareTo(pair.Value) >= 0 && (highest is null || pair.Key > highest.Value))
            {
                highest = pair.Key;
            }
        }
        return highest;
    }

    private static long NextTarget<T>(IReadOnlyDictionary<BadgeLevel, T> thresholds, T current)
        where T : IComparable<T>
    {
        long next = long.MaxValue;
        foreach (var pair in thresholds)
        {
            if (current.CompareTo(pair.Value) < 0)
            {
                var numeric = Convert.ToInt64(pair.Value);
                if (numeric < next) next = numeric;
            }
        }
        return next;
    }
}