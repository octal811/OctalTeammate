using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.MajorTask.GetMajorTasksByTrack;

public class GetMajorTasksByTrackQueryHandler : IRequestHandler<GetMajorTasksByTrackQuery, GetMajorTasksByTrackResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProgressCalculator _progressCalculator;

    public GetMajorTasksByTrackQueryHandler(IUnitOfWork unitOfWork, IProgressCalculator progressCalculator)
    {
        _unitOfWork = unitOfWork;
        _progressCalculator = progressCalculator;
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

        var progressMap = await _progressCalculator.GetMajorTasksProgressAsync(
            tasks.Select(t => t.Id),
            cancellationToken);

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
                progressMap.GetValueOrDefault(t.Id),
                t.CreatedByUserId,
                t.DeletedByUserId,
                t.CreatedDate))
            .ToList();

        return new GetMajorTasksByTrackResponse(items);
    }
}
