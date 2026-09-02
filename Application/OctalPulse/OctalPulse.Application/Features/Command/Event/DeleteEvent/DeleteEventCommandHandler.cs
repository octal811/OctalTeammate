using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Event.DeleteEvent;

public class DeleteEventCommandHandler : IRequestHandler<DeleteEventCommand, Unit>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public DeleteEventCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<Unit> Handle(DeleteEventCommand request, CancellationToken cancellationToken)
    {
        var existing = await _unitOfWork.Events.FirstOrDefaultAsync(
            e => e.Id == request.Id,
            cancellationToken);

        if (existing is null)
            throw new NotFoundException("Event not found.");

        if (existing.ProjectId is null)
            throw new NotFoundException("Event not found.");

        var isMember = await _unitOfWork.ProjectMembers.AnyAsync(
            m => m.ProjectId == existing.ProjectId.Value &&
                 m.UserId == request.UserId &&
                 m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Only approved project members can delete events of this project.");

        if (existing.CreatedByUserId != request.UserId)
            throw new ForbiddenException("Only the event creator can delete this event.");

        if (!existing.IsDeleted)
        {
            existing.IsDeleted = true;
            existing.DeletedByUserId = request.UserId;
            existing.ModifiedDate = DateTime.UtcNow;
            _unitOfWork.Events.Update(existing);

            await _unitOfWork.CompleteAsync(cancellationToken);

            await _realtimeNotifier.EventChangedAsync(existing.ProjectId.Value, existing.Id, cancellationToken);
        }

        return Unit.Value;
    }
}
