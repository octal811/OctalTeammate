using MediatR;
using OctalPulse.Application.Exceptions;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.Event.GetEventsByMonth;

public class GetEventsByMonthQueryHandler : IRequestHandler<GetEventsByMonthQuery, GetEventsByMonthResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetEventsByMonthQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetEventsByMonthResponse> Handle(
        GetEventsByMonthQuery request,
        CancellationToken cancellationToken)
    {
        var projectExists = await _unitOfWork.Projects.AnyAsync(
            p => p.Id == request.ProjectId,
            cancellationToken);

        if (!projectExists)
            throw new NotFoundException("Project not found.");

        var isMember = await _unitOfWork.ProjectMembers.AnyAsync(
            m => m.ProjectId == request.ProjectId &&
                 m.UserId == request.UserId &&
                 m.Status == MembershipStatus.Approved,
            cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Only approved project members can view events of this project.");

        var events = (await _unitOfWork.Events.FindAsync(
            e => e.ProjectId == request.ProjectId &&
                 e.StartDate.Year == request.Year &&
                 e.StartDate.Month == request.Month,
            cancellationToken)).ToList();

        var items = events
            .OrderBy(e => e.StartDate)
            .ThenBy(e => e.CreatedDate)
            .Select(e => new EventItem(
                e.Id,
                e.ProjectId,
                e.Title,
                e.Description,
                e.Type,
                e.StartDate,
                e.EndDate,
                e.StartTime,
                e.EndTime,
                e.IsAllDay,
                e.TrackId,
                e.MajorTaskId,
                e.CreatedByUserId,
                e.DeletedByUserId,
                e.IsDeleted,
                e.CreatedDate,
                e.ModifiedDate))
            .ToList();

        return new GetEventsByMonthResponse(
            items,
            request.Year,
            request.Month,
            items.Count);
    }
}
