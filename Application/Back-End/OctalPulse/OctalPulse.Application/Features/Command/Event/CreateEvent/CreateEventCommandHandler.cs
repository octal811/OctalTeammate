using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.Event.CreateEvent;

public class CreateEventCommandHandler : IRequestHandler<CreateEventCommand, CreateEventResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public CreateEventCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<CreateEventResponse> Handle(CreateEventCommand request, CancellationToken cancellationToken)
    {
        var projectExists = await _unitOfWork.Projects.AnyAsync(
            p => p.Id == request.ProjectId,
            cancellationToken);

        if (!projectExists)
            throw new NotFoundException("Project not found.");

        var isMember = await _unitOfWork.ProjectMembers.AnyAsync(
            m => m.ProjectId == request.ProjectId &&
                 m.UserId == request.CreatedByUserId &&
                 m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Only approved project members can add events to this project.");

        var now = DateTime.UtcNow;

        var eventEntity = new Domain.Entities.Event
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            Title = request.Title,
            Description = request.Description,
            Type = request.Type,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsAllDay = request.IsAllDay,
            TrackId = request.TrackId,
            MajorTaskId = request.MajorTaskId,
            CreatedByUserId = request.CreatedByUserId,
            IsDeleted = false,
            CreatedDate = now
        };

        await _unitOfWork.Events.AddAsync(eventEntity, cancellationToken);
        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.EventChangedAsync(request.ProjectId, eventEntity.Id, cancellationToken);

        return new CreateEventResponse(
            eventEntity.Id,
            eventEntity.ProjectId,
            eventEntity.Title,
            eventEntity.Description,
            eventEntity.Type,
            eventEntity.StartDate,
            eventEntity.EndDate,
            eventEntity.StartTime,
            eventEntity.EndTime,
            eventEntity.IsAllDay,
            eventEntity.TrackId,
            eventEntity.MajorTaskId,
            eventEntity.CreatedByUserId,
            eventEntity.IsDeleted,
            eventEntity.CreatedDate);
    }
}
