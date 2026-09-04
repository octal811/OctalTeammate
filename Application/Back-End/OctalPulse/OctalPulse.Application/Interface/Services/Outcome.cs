using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Interface.Services;

public record Outcome(
    Guid Id,
    Guid? UserId,
    Priority Priority,
    string APIRequested,
    OutcomeState State,
    string Details,
    DateTime Date);
