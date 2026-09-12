using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.MinorTask.UpdateMinorTask;

public class UpdateMinorTaskCommandHandler : IRequestHandler<UpdateMinorTaskCommand, UpdateMinorTaskResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly IBadgeService _badgeService;

    public UpdateMinorTaskCommandHandler(
        IUnitOfWork unitOfWork,
        IRealtimeNotifier realtimeNotifier,
        IBadgeService badgeService)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
        _badgeService = badgeService;
    }

    public async Task<UpdateMinorTaskResponse> Handle(
        UpdateMinorTaskCommand request,
        CancellationToken cancellationToken)
    {
        var task = await _unitOfWork.MinorTasks.GetByIdAsync(request.Id, cancellationToken);
        if (task is null)
            throw new NotFoundException("Minor task not found.");

        var majorTask = await _unitOfWork.MajorTasks.GetByIdAsync(task.MajorTaskId, cancellationToken);
        if (majorTask is null)
            throw new NotFoundException("Major task not found.");

        await EnsureApprovedTrackMemberAsync(majorTask.TrackId, request.UserId, cancellationToken);

        if (task.CreatedByUserId != request.UserId)
            throw new ForbiddenException("Only the creator of this minor task can update it.");

        var previousState = task.State;
        var previousTotalSeconds = task.WorkTime?.TotalSeconds ?? 0;

        task.Title = request.Title;
        task.Description = request.Description;
        task.Target = request.Target;
        task.State = request.State;
        task.JobType = request.JobType;
        task.Notes = request.Notes;
        task.Link = request.Link;
        task.Order = request.Order;
        task.AssignedUserId = request.AssignedUserId;

        if (request.WorkTimeSeconds is long seconds)
            task.WorkTime = TimeSpan.FromSeconds(Math.Max(0, seconds));
        task.CompletedDate = request.State == MinorTaskState.Done
            ? (previousState == MinorTaskState.Done ? task.CompletedDate : DateTime.UtcNow)
            : null;
        task.ModifiedDate = DateTime.UtcNow;

        var justCompletedSolveBug = task.JobType == MinorTaskJobType.SolveBug
            && task.State == MinorTaskState.Done
            && previousState != MinorTaskState.Done;

        _unitOfWork.MinorTasks.Update(task);

        var deltaSeconds = (long)Math.Floor(task.WorkTime?.TotalSeconds ?? 0) - (long)Math.Floor(previousTotalSeconds);
        if (deltaSeconds > 0)
        {
            await AddToDailyLedgerAsync(request.UserId, deltaSeconds, cancellationToken);
            await _badgeService.EvaluateWorkTitanAsync(request.UserId, cancellationToken);
            await _badgeService.EvaluateStreakMasterAsync(request.UserId, cancellationToken);
        }

        await _badgeService.EvaluateBugHunterAsync(request.UserId, justCompletedSolveBug, cancellationToken);
        await _badgeService.EvaluateTaskFinisherAsync(request.UserId, justCompletedSolveBug, cancellationToken);
        await _badgeService.EvaluateAllRounderAsync(request.UserId, task.JobType, cancellationToken);
        if (task.State == MinorTaskState.Done)
        {
            await _badgeService.EvaluateCriticalFocusAsync(request.UserId, cancellationToken);
        }
        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.MinorTaskChangedAsync(majorTask.TrackId, task.Id, cancellationToken);

        return new UpdateMinorTaskResponse(
            task.Id,
            task.MajorTaskId,
            task.Title,
            task.Description,
            task.Target,
            task.State,
            task.JobType,
            task.Notes,
            task.Link,
            task.Order,
            task.WorkTime is null ? null : (long)task.WorkTime.Value.TotalSeconds,
            task.AssignedUserId,
            task.CreatedByUserId,
            task.ModifiedDate);
    }

    private async Task EnsureApprovedTrackMemberAsync(Guid trackId, Guid userId, CancellationToken cancellationToken)
    {
        var isMember = await _unitOfWork.TrackMembers.AnyAsync(
            m => m.TrackId == trackId && m.UserId == userId && m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Only approved track members can update minor tasks in this track.");
    }

    private async Task AddToDailyLedgerAsync(Guid userId, long deltaSeconds, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(utcNow);

        var log = await _unitOfWork.DailyWorkLogs.GetByUserAndDateAsync(userId, today, cancellationToken);
        if (log is null)
        {
            log = new Domain.Entities.DailyWorkLog
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
    }
}
