using MediatR;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Query.Activity.GetMonthlyActivity;

public class GetMonthlyActivityQueryHandler : IRequestHandler<GetMonthlyActivityQuery, GetMonthlyActivityResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMonthlyActivityQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetMonthlyActivityResponse> Handle(
        GetMonthlyActivityQuery request,
        CancellationToken cancellationToken)
    {
        var from = new DateOnly(request.Year, request.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(request.Year, request.Month);
        var to = from.AddDays(daysInMonth - 1);
        var monthEndUtcExclusive = from.ToDateTime(TimeOnly.MinValue).AddMonths(1);

        var workLogs = (await _unitOfWork.DailyWorkLogs.GetRangeAsync(
                request.UserId, from, to, cancellationToken))
            .ToList();

        var workSecondsByDate = workLogs
            .GroupBy(l => l.WorkDate)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.TotalSeconds));

        var doneTasks = (await _unitOfWork.MinorTasks.FindAsync(
                mn => mn.CreatedByUserId == request.UserId
                    && mn.State == MinorTaskState.Done
                    && mn.CompletedDate.HasValue
                    && mn.CompletedDate.Value >= MonthStartUtc(from)
                    && mn.CompletedDate.Value < monthEndUtcExclusive,
                cancellationToken))
            .ToList();

        var tasksByDate = doneTasks
            .GroupBy(mn => DateOnly.FromDateTime(mn.CompletedDate!.Value))
            .ToDictionary(g => g.Key, g => g.Count());

        var days = new List<DailyActivityResponse>(daysInMonth);
        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(request.Year, request.Month, day);
            days.Add(new DailyActivityResponse(
                date,
                workSecondsByDate.GetValueOrDefault(date),
                tasksByDate.GetValueOrDefault(date)));
        }

        return new GetMonthlyActivityResponse(request.Year, request.Month, days);
    }

    private static DateTime MonthStartUtc(DateOnly from) => from.ToDateTime(TimeOnly.MinValue);
}