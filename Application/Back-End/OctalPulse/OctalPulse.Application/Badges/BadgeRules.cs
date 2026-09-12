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

    // Work Titan — total work time ever logged (in seconds)
    public static IReadOnlyDictionary<BadgeLevel, long> WorkTitanThresholdSeconds { get; } =
        new Dictionary<BadgeLevel, long>
        {
            [BadgeLevel.Bronze] = 3 * 60 * 60,          // 3 hours
            [BadgeLevel.Silver] = 6 * 60 * 60,          // 6 hours
            [BadgeLevel.Gold] = 24 * 60 * 60,           // 24 hours
            [BadgeLevel.Platinum] = 64 * 60 * 60,       // 64 hours
            [BadgeLevel.Diamond] = 128 * 60 * 60,       // 128 hours
            [BadgeLevel.Legend] = 380 * 60 * 60         // 380 hours
        };

    // Streak Master — longest consecutive days with logged work
    public static IReadOnlyDictionary<BadgeLevel, int> StreakMasterThresholdDays { get; } =
        new Dictionary<BadgeLevel, int>
        {
            [BadgeLevel.Bronze] = 3,
            [BadgeLevel.Silver] = 7,
            [BadgeLevel.Gold] = 18,
            [BadgeLevel.Platinum] = 26,
            [BadgeLevel.Diamond] = 32,
            [BadgeLevel.Legend] = 60
        };

    // Task Finisher — total completed (Done) minor tasks you created
    public static IReadOnlyDictionary<BadgeLevel, int> TaskFinisherThresholdCount { get; } =
        new Dictionary<BadgeLevel, int>
        {
            [BadgeLevel.Bronze] = 10,
            [BadgeLevel.Silver] = 25,
            [BadgeLevel.Gold] = 50,
            [BadgeLevel.Platinum] = 100,
            [BadgeLevel.Diamond] = 200,
            [BadgeLevel.Legend] = 500
        };

    // All Rounder — distinct job types with at least one completed task (max 7)
    public static IReadOnlyDictionary<BadgeLevel, int> AllRounderThresholdTypes { get; } =
        new Dictionary<BadgeLevel, int>
        {
            [BadgeLevel.Bronze] = 2,
            [BadgeLevel.Silver] = 3,
            [BadgeLevel.Gold] = 4,
            [BadgeLevel.Platinum] = 5,
            [BadgeLevel.Diamond] = 6,
            [BadgeLevel.Legend] = 7
        };

    // Community Voice — posts and comments you authored (combined)
    public static IReadOnlyDictionary<BadgeLevel, int> CommunityVoiceThresholdCount { get; } =
        new Dictionary<BadgeLevel, int>
        {
            [BadgeLevel.Bronze] = 5,
            [BadgeLevel.Silver] = 15,
            [BadgeLevel.Gold] = 40,
            [BadgeLevel.Platinum] = 100,
            [BadgeLevel.Diamond] = 200,
            [BadgeLevel.Legend] = 500
        };

    // Team Captain — tracks where you are the lead
    public static IReadOnlyDictionary<BadgeLevel, int> TeamCaptainThresholdCount { get; } =
        new Dictionary<BadgeLevel, int>
        {
            [BadgeLevel.Bronze] = 1,
            [BadgeLevel.Silver] = 2,
            [BadgeLevel.Gold] = 3,
            [BadgeLevel.Platinum] = 4,
            [BadgeLevel.Diamond] = 6,
            [BadgeLevel.Legend] = 8
        };

    // Team Organizer — most major tasks you created within a single track
    public static IReadOnlyDictionary<BadgeLevel, int> TeamOrganizerThresholdCount { get; } =
        new Dictionary<BadgeLevel, int>
        {
            [BadgeLevel.Bronze] = 2,
            [BadgeLevel.Silver] = 4,
            [BadgeLevel.Gold] = 7,
            [BadgeLevel.Platinum] = 12,
            [BadgeLevel.Diamond] = 16,
            [BadgeLevel.Legend] = 24
        };

    public static BadgeLevel? CriticalFocusLevelFor(long seconds)
        => HighestLevelWhere(CriticalFocusThresholdSeconds, seconds);

    public static BadgeLevel? HeavyWorkLevelFor(int percent)
        => HighestLevelWhere(HeavyWorkThresholdPercent, percent);

    public static BadgeLevel? BugHunterLevelFor(int count)
        => HighestLevelWhere(BugHunterThresholdCount, count);

    public static BadgeLevel? WorkTitanLevelFor(long seconds)
        => HighestLevelWhere(WorkTitanThresholdSeconds, seconds);

    public static BadgeLevel? StreakMasterLevelFor(int days)
        => HighestLevelWhere(StreakMasterThresholdDays, days);

    public static BadgeLevel? TaskFinisherLevelFor(int count)
        => HighestLevelWhere(TaskFinisherThresholdCount, count);

    public static BadgeLevel? AllRounderLevelFor(int types)
        => HighestLevelWhere(AllRounderThresholdTypes, types);

    public static BadgeLevel? CommunityVoiceLevelFor(int count)
        => HighestLevelWhere(CommunityVoiceThresholdCount, count);

    public static BadgeLevel? TeamCaptainLevelFor(int count)
        => HighestLevelWhere(TeamCaptainThresholdCount, count);

    public static BadgeLevel? TeamOrganizerLevelFor(int count)
        => HighestLevelWhere(TeamOrganizerThresholdCount, count);

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

    public static long NextWorkTitanTarget(long currentSeconds)
        => NextTarget(WorkTitanThresholdSeconds, currentSeconds);

    public static long NextStreakMasterTarget(int currentDays)
        => NextTarget(StreakMasterThresholdDays, currentDays);

    public static long NextTaskFinisherTarget(int currentCount)
        => NextTarget(TaskFinisherThresholdCount, currentCount);

    public static long NextAllRounderTarget(int currentTypes)
        => NextTarget(AllRounderThresholdTypes, currentTypes);

    public static long NextCommunityVoiceTarget(int currentCount)
        => NextTarget(CommunityVoiceThresholdCount, currentCount);

    public static long NextTeamCaptainTarget(int currentCount)
        => NextTarget(TeamCaptainThresholdCount, currentCount);

    public static long NextTeamOrganizerTarget(int currentCount)
        => NextTarget(TeamOrganizerThresholdCount, currentCount);

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