using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Event.UpdateEvent;

public class UpdateEventCommandHandler : IRequestHandler<UpdateEventCommand, UpdateEventResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public UpdateEventCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<UpdateEventResponse> Handle(UpdateEventCommand request, CancellationToken cancellationToken)
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
            throw new ForbiddenException("Only approved project members can modify events of this project.");

        if (existing.CreatedByUserId != request.UserId)
            throw new ForbiddenException("Only the event creator can update this event.");

        existing.Title = request.Title;
        existing.Description = request.Description;
        existing.Type = request.Type;
        existing.StartDate = request.StartDate;
        existing.EndDate = request.EndDate;
        existing.StartTime = request.StartTime;
        existing.EndTime = request.EndTime;
        existing.IsAllDay = request.IsAllDay;
        existing.TrackId = request.TrackId;
        existing.MajorTaskId = request.MajorTaskId;
        existing.ModifiedDate = DateTime.UtcNow;

        _unitOfWork.Events.Update(existing);
        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.EventChangedAsync(existing.ProjectId.Value, existing.Id, cancellationToken);

        return new UpdateEventResponse(
            existing.Id,
            existing.ProjectId,
            existing.Title,
            existing.Description,
            existing.Type,
            existing.StartDate,
            existing.EndDate,
            existing.StartTime,
            existing.EndTime,
            existing.IsAllDay,
            existing.TrackId,
            existing.MajorTaskId,
            existing.CreatedByUserId,
            existing.ModifiedDate);
    }
}
