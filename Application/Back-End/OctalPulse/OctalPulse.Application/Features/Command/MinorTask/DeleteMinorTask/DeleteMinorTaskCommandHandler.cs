using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.MinorTask.DeleteMinorTask;

public class DeleteMinorTaskCommandHandler : IRequestHandler<DeleteMinorTaskCommand, Unit>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public DeleteMinorTaskCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<Unit> Handle(DeleteMinorTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _unitOfWork.MinorTasks.GetByIdAsync(request.Id, cancellationToken);
        if (task is null)
            throw new NotFoundException("Minor task not found.");

        var majorTask = await _unitOfWork.MajorTasks.GetByIdAsync(task.MajorTaskId, cancellationToken);
        if (majorTask is null)
            throw new NotFoundException("Major task not found.");

        await EnsureApprovedTrackMemberAsync(majorTask.TrackId, request.UserId, cancellationToken);

        if (task.CreatedByUserId != request.UserId)
            throw new ForbiddenException("Only the creator of this minor task can delete it.");

        if (!task.IsDeleted)
        {
            var now = DateTime.UtcNow;
            task.IsDeleted = true;
            task.DeletedByUserId = request.UserId;
            task.ModifiedDate = now;
            _unitOfWork.MinorTasks.Update(task);

            await _unitOfWork.CompleteAsync(cancellationToken);

            await _realtimeNotifier.MinorTaskChangedAsync(majorTask.TrackId, task.Id, cancellationToken);
        }

        return Unit.Value;
    }

    private async Task EnsureApprovedTrackMemberAsync(Guid trackId, Guid userId, CancellationToken cancellationToken)
    {
        var isMember = await _unitOfWork.TrackMembers.AnyAsync(
            m => m.TrackId == trackId && m.UserId == userId && m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Only approved track members can delete minor tasks in this track.");
    }
}
