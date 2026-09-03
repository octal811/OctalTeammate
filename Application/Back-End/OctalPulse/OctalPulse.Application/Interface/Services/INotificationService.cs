namespace OctalPulse.Application.Interface.Services;

public enum OtpPurpose
{
    EmailVerification,
    PasswordReset,
    Message
}

public interface INotificationService
{
    Task SendOtpAsync(
        string to,
        string otp,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default);

    Task SendEmailVerificationAsync(
        string to,
        string otp,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(
        string to,
        string otp,
        CancellationToken cancellationToken = default);

    Task SendTaskNotificationAsync(
        string to,
        string taskTitle,
        string message,
        string? link = null,
        CancellationToken cancellationToken = default);

    Task SendTaskNotificationAsync(
        IEnumerable<string> recipients,
        string taskTitle,
        string message,
        string? link = null,
        CancellationToken cancellationToken = default);
}