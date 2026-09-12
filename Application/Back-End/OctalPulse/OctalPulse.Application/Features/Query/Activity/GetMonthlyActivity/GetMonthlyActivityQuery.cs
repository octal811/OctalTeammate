namespace OctalPulse.Application.Features.Query.Activity.GetMonthlyActivity;

public record GetMonthlyActivityQuery(Guid UserId, int Year, int Month)
    : MediatR.IRequest<GetMonthlyActivityResponse>;

public record DailyActivityResponse(DateOnly Date, long WorkSeconds, int CompletedTasks);

public record GetMonthlyActivityResponse(int Year, int Month, IReadOnlyList<DailyActivityResponse> Days);