using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.MinorTask.CreateMinorTask;

public class CreateMinorTaskCommandHandler : IRequestHandler<CreateMinorTaskCommand, CreateMinorTaskResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly IAppNotificationService _notificationService;

    public CreateMinorTaskCommandHandler(
        IUnitOfWork unitOfWork,
        IRealtimeNotifier realtimeNotifier,
        IAppNotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
        _notificationService = notificationService;
    }

    public async Task<CreateMinorTaskResponse> Handle(
        CreateMinorTaskCommand request,
        CancellationToken cancellationToken)
    {
        var majorTask = await _unitOfWork.MajorTasks.GetByIdAsync(request.MajorTaskId, cancellationToken);
        if (majorTask is null)
            throw new NotFoundException("Major task not found.");

        await EnsureApprovedTrackMemberAsync(majorTask.TrackId, request.UserId, cancellationToken);

        var now = DateTime.UtcNow;

        var task = new Domain.Entities.MinorTask
        {
            Id = Guid.NewGuid(),
            MajorTaskId = request.MajorTaskId,
            Title = request.Title,
            Description = request.Description,
            Target = request.Target,
            State = request.State,
            JobType = request.JobType,
            Notes = request.Notes,
            Link = request.Link,
            Order = request.Order,
            AssignedUserId = request.AssignedUserId,
            CreatedByUserId = request.UserId,
            CompletedDate = request.State == MinorTaskState.Done ? now : null,
            IsDeleted = false,
            CreatedDate = now
        };

        await _unitOfWork.MinorTasks.AddAsync(task, cancellationToken);
        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.MinorTaskChangedAsync(majorTask.TrackId, task.Id, cancellationToken);

        var track = await _unitOfWork.Tracks.GetByIdAsync(majorTask.TrackId, cancellationToken);
        var trackName = track?.Name ?? "track";

        await _notificationService.NotifyTrackMembersAsync(
            majorTask.TrackId,
            request.UserId,
            "New Sub-Task",
            $"Added sub-task '{task.Title}' under '{majorTask.Title}' in track '{trackName}'.",
            Domain.Enums.NotificationType.MinorTask,
            task.Id,
            cancellationToken);

        if (task.AssignedUserId.HasValue && task.AssignedUserId != request.UserId)
        {
            await _notificationService.CreateAndSendNotificationAsync(
                task.AssignedUserId.Value,
                request.UserId,
                track?.ProjectId,
                majorTask.TrackId,
                "Sub-Task Assigned",
                $"You were assigned sub-task '{task.Title}'.",
                Domain.Enums.NotificationType.MinorTask,
                task.Id,
                cancellationToken);
        }

        return new CreateMinorTaskResponse(
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
            task.IsDeleted,
            task.CreatedDate);
    }

    private async Task EnsureApprovedTrackMemberAsync(Guid trackId, Guid userId, CancellationToken cancellationToken)
    {
        var isMember = await _unitOfWork.TrackMembers.AnyAsync(
            m => m.TrackId == trackId && m.UserId == userId && m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Only approved track members can add minor tasks to this track.");
    }
}
