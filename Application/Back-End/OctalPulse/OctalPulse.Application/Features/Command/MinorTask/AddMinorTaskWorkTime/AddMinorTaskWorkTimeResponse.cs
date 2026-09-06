namespace OctalPulse.Application.Features.Command.MinorTask.AddMinorTaskWorkTime;

public record AddMinorTaskWorkTimeResponse(
    Guid Id,
    long WorkTimeSeconds);