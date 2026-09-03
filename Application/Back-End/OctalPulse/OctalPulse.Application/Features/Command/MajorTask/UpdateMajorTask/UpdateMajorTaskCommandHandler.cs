using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.MajorTask.UpdateMajorTask;

public class UpdateMajorTaskCommandHandler : IRequestHandler<UpdateMajorTaskCommand, UpdateMajorTaskResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public UpdateMajorTaskCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<UpdateMajorTaskResponse> Handle(
        UpdateMajorTaskCommand request,
        CancellationToken cancellationToken)
    {
        var task = await _unitOfWork.MajorTasks.GetByIdAsync(request.Id, cancellationToken);
        if (task is null)
            throw new NotFoundException("Major task not found.");

        await EnsureApprovedTrackMemberAsync(task.TrackId, request.UserId, cancellationToken);

        task.Title = request.Title;
        task.Description = request.Description;
        task.Details = request.Details;
        task.Link = request.Link;
        task.State = request.State;
        task.Priority = request.Priority;
        task.DueDate = request.DueDate;
        task.Order = request.Order;
        task.AssignedUserId = request.AssignedUserId;
        task.ModifiedDate = DateTime.UtcNow;

        _unitOfWork.MajorTasks.Update(task);
        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.MajorTaskChangedAsync(task.TrackId, task.Id, cancellationToken);

        return new UpdateMajorTaskResponse(
            task.Id,
            task.TrackId,
            task.Title,
            task.Description,
            task.Details,
            task.Link,
            task.State,
            task.Priority,
            task.DueDate,
            task.Order,
            task.AssignedUserId,
            task.Progress,
            task.CreatedByUserId,
            task.DeletedByUserId,
            task.ModifiedDate);
    }

    private async Task EnsureApprovedTrackMemberAsync(Guid trackId, Guid userId, CancellationToken cancellationToken)
    {
        var isMember = await _unitOfWork.TrackMembers.AnyAsync(
            m => m.TrackId == trackId && m.UserId == userId && m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Only approved track members can update major tasks in this track.");
    }
}
