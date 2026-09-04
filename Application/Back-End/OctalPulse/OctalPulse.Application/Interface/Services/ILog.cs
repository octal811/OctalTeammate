using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Interface.Services;

/// <summary>
/// Application-wide logging contract. Implemented by a concrete logging library
/// (currently Serilog). To switch providers, add another implementation (e.g. for
/// NLog or Microsoft ILogger) and register it in DI — no consuming code changes.
/// </summary>
public interface ILog
{
    void Trace(string message, params object?[] args);
    void Debug(string message, params object?[] args);
    void Information(string message, params object?[] args);
    void Warning(string message, params object?[] args);
    void Error(string message, Exception? exception = null, params object?[] args);

    /// <summary>Writes a fully-specified outcome.</summary>
    void WriteOutcome(Outcome outcome);

    /// <summary>
    /// Convenience overload: builds an outcome from the essential pieces.
    /// When <paramref name="priority"/> is null it is derived from <paramref name="state"/>.
    /// </summary>
    void Write(
        OutcomeState state,
        string apiRequested,
        string details,
        Priority? priority = null,
        Guid? userId = null);
}