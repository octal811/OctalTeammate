using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.MinorTask.GetMinorTasksByMajorTask;

public class GetMinorTasksByMajorTaskQueryHandler : IRequestHandler<GetMinorTasksByMajorTaskQuery, GetMinorTasksByMajorTaskResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMinorTasksByMajorTaskQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetMinorTasksByMajorTaskResponse> Handle(
        GetMinorTasksByMajorTaskQuery request,
        CancellationToken cancellationToken)
    {
        var majorTask = await _unitOfWork.MajorTasks.GetByIdAsync(request.MajorTaskId, cancellationToken);
        if (majorTask is null)
            throw new NotFoundException("Major task not found.");

        var isMember = await _unitOfWork.TrackMembers.AnyAsync(
            m => m.TrackId == majorTask.TrackId && m.UserId == request.UserId && m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Only approved track members can view minor tasks in this track.");

        var tasks = (await _unitOfWork.MinorTasks.FindAsync(
            m => m.MajorTaskId == request.MajorTaskId,
            cancellationToken)).ToList();

        var items = tasks
            .OrderBy(m => m.Order)
            .ThenByDescending(m => m.CreatedDate)
            .Select(m => new MinorTaskItem(
                m.Id,
                m.MajorTaskId,
                m.Title,
                m.Description,
                m.Target,
                m.State,
                m.Notes,
                m.Link,
                m.Order,
                m.AssignedUserId,
                m.CreatedByUserId,
                m.IsDeleted,
                m.CreatedDate))
            .ToList();

        return new GetMinorTasksByMajorTaskResponse(items);
    }
}
