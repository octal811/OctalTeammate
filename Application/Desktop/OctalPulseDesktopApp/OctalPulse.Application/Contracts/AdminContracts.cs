namespace OctalPulse.Application.Contracts;

public record AvailabilityRequest(string Key);

public record AvailabilityResponse(string Key, bool IsEnabled);

public record ApiAvailabilityStatus(
    string Key,
    bool IsEnabled,
    DateTime? UpdatedAt,
    Guid? UpdatedBy);

public record ApiErrorResponse(
    string? Error,
    string? Message,
    int? StatusCode,
    string? Code,
    string? Detail);
