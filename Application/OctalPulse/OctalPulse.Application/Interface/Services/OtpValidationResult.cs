namespace OctalPulse.Application.Interface.Services;

public enum OtpValidationResult
{
    Valid,
    Invalid,
    Expired,
    TooManyAttempts
}