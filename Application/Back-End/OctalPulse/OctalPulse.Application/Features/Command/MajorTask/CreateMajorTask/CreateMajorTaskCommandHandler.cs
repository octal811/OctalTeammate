using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Command.MajorTask.CreateMajorTask;

public class CreateMajorTaskCommandHandler : IRequestHandler<CreateMajorTaskCommand, CreateMajorTaskResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly IBadgeService _badgeService;

    public CreateMajorTaskCommandHandler(
        IUnitOfWork unitOfWork,
        IRealtimeNotifier realtimeNotifier,
        IBadgeService badgeService)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
        _badgeService = badgeService;
    }

    public async Task<CreateMajorTaskResponse> Handle(
        CreateMajorTaskCommand request,
        CancellationToken cancellationToken)
    {
        await EnsureApprovedTrackMemberAsync(request.TrackId, request.UserId, cancellationToken);

        var track = await _unitOfWork.Tracks.GetByIdAsync(request.TrackId, cancellationToken);
        if (track is null)
            throw new NotFoundException("Track not found.");

        var now = DateTime.UtcNow;

        var task = new Domain.Entities.MajorTask
        {
            Id = Guid.NewGuid(),
            TrackId = request.TrackId,
            Title = request.Title,
            Description = request.Description,
            Details = request.Details,
            Link = request.Link,
            State = request.State,
            Priority = request.Priority,
            DueDate = request.DueDate,
            Order = request.Order,
            AssignedUserId = request.AssignedUserId,
            CreatedByUserId = request.UserId,
            IsDeleted = false,
            CreatedDate = now
        };

        await _unitOfWork.MajorTasks.AddAsync(task, cancellationToken);
        await _badgeService.EvaluateTeamOrganizerAsync(request.UserId, request.TrackId, cancellationToken);
        await _unitOfWork.CompleteAsync(cancellationToken);

        await _realtimeNotifier.MajorTaskChangedAsync(request.TrackId, task.Id, cancellationToken);

        return new CreateMajorTaskResponse(
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
            0,
            task.CreatedByUserId,
            task.CreatedDate);
    }

    private async Task EnsureApprovedTrackMemberAsync(Guid trackId, Guid userId, CancellationToken cancellationToken)
    {
        var isMember = await _unitOfWork.TrackMembers.AnyAsync(
            m => m.TrackId == trackId && m.UserId == userId && m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Only approved track members can add major tasks to this track.");
    }
}
