using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Enums;
using Serilog;
using Serilog.Context;

namespace OctalPulse.API.Services;

public class SerilogLogger : ILog
{
    private const string OutcomeMessage =
        "[OUTCOME] Id={OutcomeId} UserId={OutcomeUserId} Priority={OutcomePriority} " +
        "State={OutcomeState} API={OutcomeApi} Date={OutcomeDate:dd/MM/yyyy_HH:mm:ss}";

    public void Trace(string message, params object?[] args)
        => Log.Verbose(message, args);

    public void Debug(string message, params object?[] args)
        => Log.Debug(message, args);

    public void Information(string message, params object?[] args)
        => Log.Information(message, args);

    public void Warning(string message, params object?[] args)
        => Log.Warning(message, args);

    public void Error(string message, Exception? exception = null, params object?[] args)
    {
        if (exception is null)
            Log.Error(message, args);
        else
            Log.Error(exception, message, args);
    }

    public void WriteOutcome(Outcome outcome)
    {
        var details = string.IsNullOrWhiteSpace(outcome.Details) ? "-" : outcome.Details;

        // Details is attached via LogContext so only sinks whose output template
        // references {Details} render it (the file sink). The console stays tidy.
        using (LogContext.PushProperty("Details", details))
        {
            switch (outcome.State)
            {
                case OutcomeState.Failed:
                    Log.Error(OutcomeMessage, outcome.Id, outcome.UserId, outcome.Priority,
                        outcome.State, outcome.APIRequested, outcome.Date);
                    break;
                case OutcomeState.Warning:
                    Log.Warning(OutcomeMessage, outcome.Id, outcome.UserId, outcome.Priority,
                        outcome.State, outcome.APIRequested, outcome.Date);
                    break;
                default:
                    Log.Information(OutcomeMessage, outcome.Id, outcome.UserId, outcome.Priority,
                        outcome.State, outcome.APIRequested, outcome.Date);
                    break;
            }
        }
    }

    public void Write(
        OutcomeState state,
        string apiRequested,
        string details,
        Priority? priority = null,
        Guid? userId = null)
    {
        WriteOutcome(new Outcome(
            Id: Guid.NewGuid(),
            UserId: userId,
            Priority: priority ?? DerivePriority(state),
            APIRequested: apiRequested,
            State: state,
            Details: details,
            Date: DateTime.Now));
    }

    private static Priority DerivePriority(OutcomeState state) => state switch
    {
        OutcomeState.Success => Priority.Low,
        OutcomeState.Warning => Priority.Medium,
        OutcomeState.Failed => Priority.High,
        _ => Priority.Low
    };
}