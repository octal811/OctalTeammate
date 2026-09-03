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

    public UpdateMinorTaskCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
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

        task.Title = request.Title;
        task.Description = request.Description;
        task.Target = request.Target;
        task.State = request.State;
        task.Notes = request.Notes;
        task.Link = request.Link;
        task.Order = request.Order;
        task.AssignedUserId = request.AssignedUserId;
        task.ModifiedDate = DateTime.UtcNow;

        _unitOfWork.MinorTasks.Update(task);
        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.MinorTaskChangedAsync(majorTask.TrackId, task.Id, cancellationToken);

        return new UpdateMinorTaskResponse(
            task.Id,
            task.MajorTaskId,
            task.Title,
            task.Description,
            task.Target,
            task.State,
            task.Notes,
            task.Link,
            task.Order,
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
}
