namespace OctalPulse.Application.Interface.Services;

public interface IOtpService
{
    string GenerateOtp(int length = 8);

    Task<string> IssueOtpAsync(
        string subject,
        OtpPurpose purpose,
        int length = 8,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default);

    Task<OtpValidationResult> ValidateOtpAsync(
        string subject,
        OtpPurpose purpose,
        string otp,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveOtpAsync(
        string subject,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default);

    Task RemoveOtpAsync(
        string subject,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default);
}