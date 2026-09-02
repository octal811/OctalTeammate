using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.MajorTask.DeleteMajorTask;

public class DeleteMajorTaskCommandHandler : IRequestHandler<DeleteMajorTaskCommand, Unit>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public DeleteMajorTaskCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<Unit> Handle(DeleteMajorTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _unitOfWork.MajorTasks.GetByIdAsync(request.Id, cancellationToken);
        if (task is null)
            throw new NotFoundException("Major task not found.");

        await EnsureApprovedTrackMemberAsync(task.TrackId, request.UserId, cancellationToken);

        if (!task.IsDeleted)
        {
            var now = DateTime.UtcNow;
            task.IsDeleted = true;
            task.DeletedByUserId = request.UserId;
            task.ModifiedDate = now;
            _unitOfWork.MajorTasks.Update(task);

            var minorTasks = (await _unitOfWork.MinorTasks.FindAsync(
                m => m.MajorTaskId == request.Id,
                cancellationToken)).ToList();

            foreach (var minorTask in minorTasks)
            {
                minorTask.IsDeleted = true;
                minorTask.DeletedByUserId = request.UserId;
                minorTask.ModifiedDate = now;
            }

            _unitOfWork.MinorTasks.UpdateRange(minorTasks);

            await _unitOfWork.CompleteAsync(cancellationToken);

            await _realtimeNotifier.MajorTaskChangedAsync(task.TrackId, task.Id, cancellationToken);
        }

        return Unit.Value;
    }

    private async Task EnsureApprovedTrackMemberAsync(Guid trackId, Guid userId, CancellationToken cancellationToken)
    {
        var isMember = await _unitOfWork.TrackMembers.AnyAsync(
            m => m.TrackId == trackId && m.UserId == userId && m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Only approved track members can delete major tasks in this track.");
    }
}
