using MediatR;
using OctalPulse.Application.Badges;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.Badges.GetMyBadges;

public class GetMyBadgesQueryHandler : IRequestHandler<GetMyBadgesQuery, GetMyBadgesResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMyBadgesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetMyBadgesResponse> Handle(GetMyBadgesQuery request, CancellationToken cancellationToken)
    {
        var earned = (await _unitOfWork.UserBadges.FindAsync(
                b => b.UserId == request.UserId, cancellationToken))
            .ToDictionary(b => b.Type);

        var criticalFocus = await GetCriticalFocusProgressAsync(request.UserId, earned, cancellationToken);
        var bugHunter = await GetBugHunterProgressAsync(request.UserId, earned, cancellationToken);
        var heavyWork = await GetHeavyWorkProgressAsync(request.UserId, earned, cancellationToken);
        var workTitan = await GetWorkTitanProgressAsync(request.UserId, earned, cancellationToken);
        var streakMaster = await GetStreakMasterProgressAsync(request.UserId, earned, cancellationToken);
        var taskFinisher = await GetTaskFinisherProgressAsync(request.UserId, earned, cancellationToken);
        var allRounder = await GetAllRounderProgressAsync(request.UserId, earned, cancellationToken);
        var communityVoice = await GetCommunityVoiceProgressAsync(request.UserId, earned, cancellationToken);
        var teamCaptain = await GetTeamCaptainProgressAsync(request.UserId, earned, cancellationToken);
        var teamOrganizer = await GetTeamOrganizerProgressAsync(request.UserId, earned, cancellationToken);

        return new GetMyBadgesResponse(new[]
        {
            criticalFocus,
            heavyWork,
            bugHunter,
            workTitan,
            streakMaster,
            taskFinisher,
            allRounder,
            communityVoice,
            teamCaptain,
            teamOrganizer
        });
    }

    private async Task<BadgeProgressResponse> GetCriticalFocusProgressAsync(
        Guid userId,
        IReadOnlyDictionary<BadgeType, Domain.Entities.UserBadge> earned,
        CancellationToken cancellationToken)
    {
        var current = await _unitOfWork.DailyWorkLogs.GetMaxSecondsAsync(userId, cancellationToken);
        var next = BadgeRules.NextCriticalFocusTarget(current);
        var level = BadgeRules.CriticalFocusLevelFor(current);
        var badge = earned.GetValueOrDefault(BadgeType.CriticalFocus);

        return Build(BadgeType.CriticalFocus, level, badge, current, next);
    }

    private async Task<BadgeProgressResponse> GetBugHunterProgressAsync(
        Guid userId,
        IReadOnlyDictionary<BadgeType, Domain.Entities.UserBadge> earned,
        CancellationToken cancellationToken)
    {
        var current = await _unitOfWork.MinorTasks.CountAsync(
            mn => mn.CreatedByUserId == userId
                && mn.JobType == MinorTaskJobType.SolveBug
                && mn.State == MinorTaskState.Done,
            cancellationToken);
        var next = BadgeRules.NextBugHunterTarget(current);
        var level = BadgeRules.BugHunterLevelFor(current);
        var badge = earned.GetValueOrDefault(BadgeType.BugHunter);

        return Build(BadgeType.BugHunter, level, badge, current, next);
    }

    private async Task<BadgeProgressResponse> GetHeavyWorkProgressAsync(
        Guid userId,
        IReadOnlyDictionary<BadgeType, Domain.Entities.UserBadge> earned,
        CancellationToken cancellationToken)
    {
        var bestPercent = await ComputeBestHeavyWorkPercentAsync(userId, cancellationToken);
        var next = BadgeRules.NextHeavyWorkTarget(bestPercent);
        var level = BadgeRules.HeavyWorkLevelFor(bestPercent);
        var badge = earned.GetValueOrDefault(BadgeType.HeavyWork);

        return Build(BadgeType.HeavyWork, level, badge, bestPercent, next);
    }

    private async Task<int> ComputeBestHeavyWorkPercentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var doneMinorTasks = (await _unitOfWork.MinorTasks.FindAsync(
                mn => mn.CreatedByUserId == userId && mn.State == MinorTaskState.Done,
                cancellationToken))
            .ToList();

        if (doneMinorTasks.Count == 0)
            return 0;

        var majorTaskIds = doneMinorTasks.Select(mn => mn.MajorTaskId).Distinct().ToList();

        var majors = (await _unitOfWork.MajorTasks.FindAsync(
                m => majorTaskIds.Contains(m.Id) && m.State == MajorTaskState.Done,
                cancellationToken))
            .ToList();

        if (majors.Count == 0)
            return 0;

        var validIds = majors.Select(m => m.Id).ToHashSet();

        var allMinor = (await _unitOfWork.MinorTasks.FindAsync(
                mn => validIds.Contains(mn.MajorTaskId),
                cancellationToken))
            .GroupBy(mn => mn.MajorTaskId)
            .ToDictionary(g => g.Key, g => g.Count());

        var doneByUser = doneMinorTasks
            .Where(mn => validIds.Contains(mn.MajorTaskId))
            .GroupBy(mn => mn.MajorTaskId)
            .ToDictionary(g => g.Key, g => g.Count());

        int best = 0;
        foreach (var id in validIds)
        {
            if (!allMinor.TryGetValue(id, out var total) || total == 0) continue;
            var done = doneByUser.GetValueOrDefault(id);
            var percent = (int)Math.Round(100.0 * done / total);
            if (percent > best) best = percent;
        }

        return best;
    }

    private async Task<BadgeProgressResponse> GetWorkTitanProgressAsync(
        Guid userId,
        IReadOnlyDictionary<BadgeType, Domain.Entities.UserBadge> earned,
        CancellationToken cancellationToken)
    {
        var current = await _unitOfWork.DailyWorkLogs.GetTotalSecondsAsync(userId, cancellationToken);
        var next = BadgeRules.NextWorkTitanTarget(current);
        var level = BadgeRules.WorkTitanLevelFor(current);
        var badge = earned.GetValueOrDefault(BadgeType.WorkTitan);

        return Build(BadgeType.WorkTitan, level, badge, current, next);
    }

    private async Task<BadgeProgressResponse> GetStreakMasterProgressAsync(
        Guid userId,
        IReadOnlyDictionary<BadgeType, Domain.Entities.UserBadge> earned,
        CancellationToken cancellationToken)
    {
        var current = await _unitOfWork.DailyWorkLogs.GetLongestActiveStreakAsync(userId, cancellationToken);
        var next = BadgeRules.NextStreakMasterTarget(current);
        var level = BadgeRules.StreakMasterLevelFor(current);
        var badge = earned.GetValueOrDefault(BadgeType.StreakMaster);

        return Build(BadgeType.StreakMaster, level, badge, current, next);
    }

    private async Task<BadgeProgressResponse> GetTaskFinisherProgressAsync(
        Guid userId,
        IReadOnlyDictionary<BadgeType, Domain.Entities.UserBadge> earned,
        CancellationToken cancellationToken)
    {
        var current = await _unitOfWork.MinorTasks.CountAsync(
            mn => mn.CreatedByUserId == userId && mn.State == MinorTaskState.Done,
            cancellationToken);
        var next = BadgeRules.NextTaskFinisherTarget(current);
        var level = BadgeRules.TaskFinisherLevelFor(current);
        var badge = earned.GetValueOrDefault(BadgeType.TaskFinisher);

        return Build(BadgeType.TaskFinisher, level, badge, current, next);
    }

    private async Task<BadgeProgressResponse> GetAllRounderProgressAsync(
        Guid userId,
        IReadOnlyDictionary<BadgeType, Domain.Entities.UserBadge> earned,
        CancellationToken cancellationToken)
    {
        var doneTasks = (await _unitOfWork.MinorTasks.FindAsync(
                mn => mn.CreatedByUserId == userId && mn.State == MinorTaskState.Done,
                cancellationToken))
            .ToList();

        var current = doneTasks
            .Where(mn => mn.JobType.HasValue)
            .Select(mn => mn.JobType!.Value)
            .Distinct()
            .Count();

        var next = BadgeRules.NextAllRounderTarget(current);
        var level = BadgeRules.AllRounderLevelFor(current);
        var badge = earned.GetValueOrDefault(BadgeType.AllRounder);

        return Build(BadgeType.AllRounder, level, badge, current, next);
    }

    private async Task<BadgeProgressResponse> GetCommunityVoiceProgressAsync(
        Guid userId,
        IReadOnlyDictionary<BadgeType, Domain.Entities.UserBadge> earned,
        CancellationToken cancellationToken)
    {
        var posts = await _unitOfWork.Posts.CountAsync(
            p => p.AuthorId == userId && !p.IsDeleted,
            cancellationToken);
        var comments = await _unitOfWork.PostComments.CountAsync(
            c => c.UserId == userId && !c.IsDeleted,
            cancellationToken);
        var current = posts + comments;

        var next = BadgeRules.NextCommunityVoiceTarget(current);
        var level = BadgeRules.CommunityVoiceLevelFor(current);
        var badge = earned.GetValueOrDefault(BadgeType.CommunityVoice);

        return Build(BadgeType.CommunityVoice, level, badge, current, next);
    }

    private async Task<BadgeProgressResponse> GetTeamCaptainProgressAsync(
        Guid userId,
        IReadOnlyDictionary<BadgeType, Domain.Entities.UserBadge> earned,
        CancellationToken cancellationToken)
    {
        var current = await _unitOfWork.Tracks.CountAsync(
            t => t.TrackLeadUserId == userId && !t.IsDeleted,
            cancellationToken);
        var next = BadgeRules.NextTeamCaptainTarget(current);
        var level = BadgeRules.TeamCaptainLevelFor(current);
        var badge = earned.GetValueOrDefault(BadgeType.TeamCaptain);

        return Build(BadgeType.TeamCaptain, level, badge, current, next);
    }

    private async Task<BadgeProgressResponse> GetTeamOrganizerProgressAsync(
        Guid userId,
        IReadOnlyDictionary<BadgeType, Domain.Entities.UserBadge> earned,
        CancellationToken cancellationToken)
    {
        var majors = (await _unitOfWork.MajorTasks.FindAsync(
                m => m.CreatedByUserId == userId && !m.IsDeleted,
                cancellationToken))
            .ToList();

        var current = majors
            .GroupBy(m => m.TrackId)
            .Max(g => (int?)g.Count()) ?? 0;

        var next = BadgeRules.NextTeamOrganizerTarget(current);
        var level = BadgeRules.TeamOrganizerLevelFor(current);
        var badge = earned.GetValueOrDefault(BadgeType.TeamOrganizer);

        return Build(BadgeType.TeamOrganizer, level, badge, current, next);
    }

    private static BadgeProgressResponse Build(
        BadgeType type,
        BadgeLevel? currentLevel,
        Domain.Entities.UserBadge? earned,
        long currentValue,
        long nextTarget)
    {
        var level = currentLevel ?? earned?.Level;
        var progress = nextTarget == long.MaxValue
            ? 100
            : (int)Math.Min(100, Math.Round(100.0 * currentValue / nextTarget));

        return new BadgeProgressResponse(
            type,
            level,
            earned?.AwardedDate,
            currentValue,
            nextTarget == long.MaxValue ? currentValue : nextTarget,
            progress);
    }
}