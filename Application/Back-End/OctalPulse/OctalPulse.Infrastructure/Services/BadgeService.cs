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