using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.MajorTask.GetMajorTasksByTrack;

public class GetMajorTasksByTrackQueryHandler : IRequestHandler<GetMajorTasksByTrackQuery, GetMajorTasksByTrackResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMajorTasksByTrackQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetMajorTasksByTrackResponse> Handle(
        GetMajorTasksByTrackQuery request,
        CancellationToken cancellationToken)
    {
        var trackExists = await _unitOfWork.Tracks.AnyAsync(
            t => t.Id == request.TrackId,
            cancellationToken);

        if (!trackExists)
            throw new NotFoundException("Track not found.");

        var isMember = await _unitOfWork.TrackMembers.AnyAsync(
            m => m.TrackId == request.TrackId && m.UserId == request.UserId && m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Only approved track members can view major tasks in this track.");

        var tasks = (await _unitOfWork.MajorTasks.FindAsync(
            t => t.TrackId == request.TrackId,
            cancellationToken)).ToList();

        var items = tasks
            .OrderBy(t => t.Order)
            .ThenByDescending(t => t.CreatedDate)
            .Select(t => new MajorTaskItem(
                t.Id,
                t.TrackId,
                t.Title,
                t.Description,
                t.Details,
                t.Link,
                t.State,
                t.Priority,
                t.DueDate,
                t.Order,
                t.AssignedUserId,
                t.Progress,
                t.CreatedByUserId,
                t.DeletedByUserId,
                t.CreatedDate))
            .ToList();

        return new GetMajorTasksByTrackResponse(items);
    }
}
