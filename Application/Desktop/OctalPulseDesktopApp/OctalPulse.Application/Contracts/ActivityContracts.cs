namespace OctalPulse.Application.Contracts;

public record DailyActivityResponse(DateOnly Date, long WorkSeconds, int CompletedTasks);

public record GetMonthlyActivityResponse(
    int Year,
    int Month,
    IReadOnlyList<DailyActivityResponse> Days);