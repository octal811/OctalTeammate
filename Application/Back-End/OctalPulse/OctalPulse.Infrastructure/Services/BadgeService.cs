using Microsoft.Extensions.Logging;
using OctalPulse.Application.Badges;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Infrastructure.Services;

public class BadgeService : IBadgeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BadgeService> _logger;

    public BadgeService(IUnitOfWork unitOfWork, ILogger<BadgeService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task EvaluateCriticalFocusAsync(Guid userId, long deltaSeconds, CancellationToken cancellationToken = default)
    {
        try
        {
            var utcNow = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(utcNow);

            var log = await _unitOfWork.DailyWorkLogs.GetByUserAndDateAsync(userId, today, cancellationToken);
            if (log is null)
            {
                log = new DailyWorkLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    WorkDate = today,
                    TotalSeconds = 0,
                    CreatedDate = utcNow,
                    UpdatedDate = utcNow,
                    IsDeleted = false
                };
                await _unitOfWork.DailyWorkLogs.AddAsync(log, cancellationToken);
            }

            log.TotalSeconds += deltaSeconds;
            log.UpdatedDate = utcNow;
            log.ModifiedDate = utcNow;

            var level = BadgeRules.CriticalFocusLevelFor(log.TotalSeconds);
            if (level is not null)
            {
                await GrantOrUpgradeAsync(userId, BadgeType.CriticalFocus, level.Value, null, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate Critical Focus badge for user {UserId}.", userId);
        }
    }

    public async Task EvaluateBugHunterAsync(Guid userId, bool justCompletedSolveBug, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _unitOfWork.MinorTasks.CountAsync(
                mn => mn.CreatedByUserId == userId
                    && mn.JobType == MinorTaskJobType.SolveBug
                    && mn.State == MinorTaskState.Done,
                cancellationToken);

            if (justCompletedSolveBug)
                count++;

            var level = BadgeRules.BugHunterLevelFor(count);
            if (level is not null)
            {
                await GrantOrUpgradeAsync(userId, BadgeType.BugHunter, level.Value, null, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate Bug Hunter badge for user {UserId}.", userId);
        }
    }

    public async Task EvaluateHeavyWorkAsync(Guid majorTaskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var allMinor = (await _unitOfWork.MinorTasks.FindAsync(
                    mn => mn.MajorTaskId == majorTaskId, cancellationToken))
                .ToList();

            if (allMinor.Count == 0)
                return;

            var doneByCreator = allMinor
                .Where(mn => mn.State == MinorTaskState.Done && mn.CreatedByUserId.HasValue)
                .GroupBy(mn => mn.CreatedByUserId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var (creatorId, done) in doneByCreator)
            {
                var percent = (int)Math.Round(100.0 * done / allMinor.Count);
                var level = BadgeRules.HeavyWorkLevelFor(percent);
                if (level is not null)
                {
                    await GrantOrUpgradeAsync(creatorId, BadgeType.HeavyWork, level.Value, majorTaskId, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate Heavy Work badge for major task {MajorTaskId}.", majorTaskId);
        }
    }

    public async Task EvaluateWorkTitanAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var totalSeconds = await _unitOfWork.DailyWorkLogs.GetTotalSecondsAsync(userId, cancellationToken);
            var level = BadgeRules.WorkTitanLevelFor(totalSeconds);
            if (level is not null)
            {
                await GrantOrUpgradeAsync(userId, BadgeType.WorkTitan, level.Value, null, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate Work Titan badge for user {UserId}.", userId);
        }
    }

    public async Task EvaluateStreakMasterAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var streak = await _unitOfWork.DailyWorkLogs.GetLongestActiveStreakAsync(userId, cancellationToken);
            var level = BadgeRules.StreakMasterLevelFor(streak);
            if (level is not null)
            {
                await GrantOrUpgradeAsync(userId, BadgeType.StreakMaster, level.Value, null, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate Streak Master badge for user {UserId}.", userId);
        }
    }

    public async Task EvaluateTaskFinisherAsync(Guid userId, bool justCompleted, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _unitOfWork.MinorTasks.CountAsync(
                mn => mn.CreatedByUserId == userId && mn.State == MinorTaskState.Done,
                cancellationToken);

            if (justCompleted)
                count++;

            var level = BadgeRules.TaskFinisherLevelFor(count);
            if (level is not null)
            {
                await GrantOrUpgradeAsync(userId, BadgeType.TaskFinisher, level.Value, null, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate Task Finisher badge for user {UserId}.", userId);
        }
    }

    public async Task EvaluateAllRounderAsync(
        Guid userId,
        MinorTaskJobType? justCompletedJobType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var doneTasks = (await _unitOfWork.MinorTasks.FindAsync(
                    mn => mn.CreatedByUserId == userId && mn.State == MinorTaskState.Done,
                    cancellationToken))
                .ToList();

            var types = doneTasks
                .Where(mn => mn.JobType.HasValue)
                .Select(mn => mn.JobType!.Value)
                .ToHashSet();

            if (justCompletedJobType.HasValue)
                types.Add(justCompletedJobType.Value);

            var level = BadgeRules.AllRounderLevelFor(types.Count);
            if (level is not null)
            {
                await GrantOrUpgradeAsync(userId, BadgeType.AllRounder, level.Value, null, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate All Rounder badge for user {UserId}.", userId);
        }
    }

    public async Task EvaluateCommunityVoiceAsync(Guid userId, bool justAdded, CancellationToken cancellationToken = default)
    {
        try
        {
            var posts = await _unitOfWork.Posts.CountAsync(
                p => p.AuthorId == userId && !p.IsDeleted,
                cancellationToken);
            var comments = await _unitOfWork.PostComments.CountAsync(
                c => c.UserId == userId && !c.IsDeleted,
                cancellationToken);

            var count = posts + comments + (justAdded ? 1 : 0);
            var level = BadgeRules.CommunityVoiceLevelFor(count);
            if (level is not null)
            {
                await GrantOrUpgradeAsync(userId, BadgeType.CommunityVoice, level.Value, null, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate Community Voice badge for user {UserId}.", userId);
        }
    }

    public async Task EvaluateTeamCaptainAsync(Guid userId, bool justCreated, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _unitOfWork.Tracks.CountAsync(
                t => t.TrackLeadUserId == userId && !t.IsDeleted,
                cancellationToken);

            if (justCreated)
                count++;

            var level = BadgeRules.TeamCaptainLevelFor(count);
            if (level is not null)
            {
                await GrantOrUpgradeAsync(userId, BadgeType.TeamCaptain, level.Value, null, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate Team Captain badge for user {UserId}.", userId);
        }
    }

    public async Task EvaluateTeamOrganizerAsync(
        Guid userId,
        Guid trackId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var majors = (await _unitOfWork.MajorTasks.FindAsync(
                    m => m.CreatedByUserId == userId && !m.IsDeleted,
                    cancellationToken))
                .ToList();

            var countsByTrack = majors
                .GroupBy(m => m.TrackId)
                .ToDictionary(g => g.Key, g => g.Count());

            countsByTrack.TryGetValue(trackId, out var addedCount);
            countsByTrack[trackId] = addedCount + 1;

            var max = countsByTrack.Values.DefaultIfEmpty(0).Max();
            var level = BadgeRules.TeamOrganizerLevelFor(max);
            if (level is not null)
            {
                await GrantOrUpgradeAsync(userId, BadgeType.TeamOrganizer, level.Value, null, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate Team Organizer badge for user {UserId}.", userId);
        }
    }

    private async Task GrantOrUpgradeAsync(
        Guid userId,
        BadgeType type,
        BadgeLevel level,
        Guid? refId,
        CancellationToken cancellationToken)
    {
        var existing = await _unitOfWork.UserBadges.GetByTypeAsync(userId, type, cancellationToken);
        if (existing is not null)
        {
            if (existing.Level >= level)
                return;

            existing.Level = level;
            existing.AwardedDate = DateTime.UtcNow;
            existing.RefId = refId;
            existing.ModifiedDate = DateTime.UtcNow;
            _unitOfWork.UserBadges.Update(existing);
        }
        else
        {
            var badge = new UserBadge
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = type,
                Level = level,
                AwardedDate = DateTime.UtcNow,
                RefId = refId,
                CreatedDate = DateTime.UtcNow,
                IsDeleted = false
            };
            await _unitOfWork.UserBadges.AddAsync(badge, cancellationToken);
        }
    }
}