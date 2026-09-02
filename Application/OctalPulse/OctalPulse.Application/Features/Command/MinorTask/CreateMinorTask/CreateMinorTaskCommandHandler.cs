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

    public CreateMinorTaskCommandHandler(IUnitOfWork unitOfWork, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
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
            Notes = request.Notes,
            Link = request.Link,
            Order = request.Order,
            AssignedUserId = request.AssignedUserId,
            CreatedByUserId = request.UserId,
            IsDeleted = false,
            CreatedDate = now
        };

        await _unitOfWork.MinorTasks.AddAsync(task, cancellationToken);
        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.MinorTaskChangedAsync(majorTask.TrackId, task.Id, cancellationToken);

        return new CreateMinorTaskResponse(
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
