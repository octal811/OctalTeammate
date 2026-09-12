using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.MinorTask.AddMinorTaskWorkTime;

public class AddMinorTaskWorkTimeCommandHandler : IRequestHandler<AddMinorTaskWorkTimeCommand, AddMinorTaskWorkTimeResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly IBadgeService _badgeService;

    public AddMinorTaskWorkTimeCommandHandler(
        IUnitOfWork unitOfWork,
        IRealtimeNotifier realtimeNotifier,
        IBadgeService badgeService)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
        _badgeService = badgeService;
    }

    public async Task<AddMinorTaskWorkTimeResponse> Handle(
        AddMinorTaskWorkTimeCommand request,
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
            throw new ForbiddenException("Only the creator of this minor task can add work time.");

        var delta = TimeSpan.FromSeconds(request.WorkTimeSeconds);
        task.WorkTime = (task.WorkTime ?? TimeSpan.Zero) + delta;
        task.ModifiedDate = DateTime.UtcNow;

        _unitOfWork.MinorTasks.Update(task);
        await _badgeService.EvaluateCriticalFocusAsync(request.UserId, request.WorkTimeSeconds, cancellationToken);
        await _badgeService.EvaluateWorkTitanAsync(request.UserId, cancellationToken);
        await _badgeService.EvaluateStreakMasterAsync(request.UserId, cancellationToken);
        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.MinorTaskChangedAsync(majorTask.TrackId, task.Id, cancellationToken);

        return new AddMinorTaskWorkTimeResponse(
            task.Id,
            (long)task.WorkTime.Value.TotalSeconds);
    }

    private async Task EnsureApprovedTrackMemberAsync(Guid trackId, Guid userId, CancellationToken cancellationToken)
    {
        var isMember = await _unitOfWork.TrackMembers.AnyAsync(
            m => m.TrackId == trackId && m.UserId == userId && m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Only approved track members can add work time in this track.");
    }
}