using Microsoft.Extensions.Options;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private static readonly TimeSpan OtpExpiry = TimeSpan.FromMinutes(10);

    private readonly IEmailService _emailService;
    private readonly string _otpExpiryLabel;
    private readonly EmailSettings _settings;

    public NotificationService(IEmailService emailService, IOptions<EmailSettings> settings)
    {
        _emailService = emailService;
        _settings = settings.Value;
        _otpExpiryLabel = $"expires in {OtpExpiry.TotalMinutes:0} minutes";
    }

    public Task SendOtpAsync(
        string to,
        string otp,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        return purpose switch
        {
            OtpPurpose.EmailVerification =>
                SendEmailVerificationAsync(to, otp, cancellationToken),
            OtpPurpose.PasswordReset =>
                SendPasswordResetAsync(to, otp, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null)
        };
    }

    public Task SendEmailVerificationAsync(
        string to,
        string otp,
        CancellationToken cancellationToken = default)
    {
        var subject = "Verify your email";
        var body = BuildOtpHtml(
            title: "Verify your email address",
            message: "Welcome to OctalTeammate! Use the code below to verify your email address. The code " + _otpExpiryLabel + ".",
            otp: otp);

        return _emailService.SendAsync(to, subject, body, cancellationToken: cancellationToken);
    }

    public Task SendPasswordResetAsync(
        string to,
        string otp,
        CancellationToken cancellationToken = default)
    {
        var subject = "Reset your password";
        var body = BuildOtpHtml(
            title: "Password reset requested",
            message: "We received a request to reset your password. Use the code below to set a new one. The code " + _otpExpiryLabel + ".",
            otp: otp);

        return _emailService.SendAsync(to, subject, body, cancellationToken: cancellationToken);
    }

    public Task SendTaskNotificationAsync(
        string to,
        string taskTitle,
        string message,
        string? link = null,
        CancellationToken cancellationToken = default)
    {
        return SendTaskNotificationAsync(
            new[] { to },
            taskTitle,
            message,
            link,
            cancellationToken);
    }

    public Task SendTaskNotificationAsync(
        IEnumerable<string> recipients,
        string taskTitle,
        string message,
        string? link = null,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Task update: {taskTitle}";
        var body = BuildTaskNotificationHtml(taskTitle, message, link);

        return _emailService.SendAsync(recipients, subject, body, cancellationToken: cancellationToken);
    }

    private string BuildOtpHtml(string title, string message, string otp)
    {
        return $"""
            <!DOCTYPE html>
            <html>
              <body style="margin:0;padding:0;background-color:#f4f5f7;font-family:Arial,sans-serif;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color:#f4f5f7;padding:40px 16px;">
                  <tr>
                    <td align="center">
                      <table role="presentation" width="560" cellspacing="0" cellpadding="0" style="background-color:#ffffff;border-radius:12px;overflow:hidden;">
                        <tr>
                          <td style="background-color:#4f46e5;padding:24px 32px;">
                            <span style="color:#ffffff;font-size:22px;font-weight:bold;">{WebUtilityHtmlEncode(_settings.FromName)}</span>
                          </td>
                        </tr>
                        <tr>
                          <td style="padding:32px;">
                            <h1 style="margin:0 0 16px;font-size:20px;color:#111827;">{WebUtilityHtmlEncode(title)}</h1>
                            <p style="margin:0 0 24px;font-size:15px;line-height:1.6;color:#374151;">{WebUtilityHtmlEncode(message)}</p>
                            <div style="text-align:center;padding:16px;background-color:#f9fafb;border-radius:8px;margin-bottom:24px;">
                              <span style="font-size:34px;font-weight:bold;letter-spacing:8px;color:#111827;">{WebUtilityHtmlEncode(otp)}</span>
                            </div>
                            <p style="margin:0;font-size:12px;color:#9ca3af;line-height:1.5;">
                              If you didn't request this, you can safely ignore this email.
                            </p>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                </table>
              </body>
            </html>
            """;
    }

    private string BuildTaskNotificationHtml(string taskTitle, string message, string? link)
    {
        var linkBlock = string.IsNullOrWhiteSpace(link)
            ? string.Empty
            : $"""
                <div style="text-align:center;margin-top:24px;">
                  <a href="{WebUtilityHtmlEncode(link)}" style="background-color:#4f46e5;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:8px;font-size:15px;font-weight:bold;display:inline-block;">View Task</a>
                </div>
                """;

        return $"""
            <!DOCTYPE html>
            <html>
              <body style="margin:0;padding:0;background-color:#f4f5f7;font-family:Arial,sans-serif;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color:#f4f5f7;padding:40px 16px;">
                  <tr>
                    <td align="center">
                      <table role="presentation" width="560" cellspacing="0" cellpadding="0" style="background-color:#ffffff;border-radius:12px;overflow:hidden;">
                        <tr>
                          <td style="background-color:#4f46e5;padding:24px 32px;">
                            <span style="color:#ffffff;font-size:22px;font-weight:bold;">{WebUtilityHtmlEncode(_settings.FromName)}</span>
                          </td>
                        </tr>
                        <tr>
                          <td style="padding:32px;">
                            <h1 style="margin:0 0 16px;font-size:20px;color:#111827;">{WebUtilityHtmlEncode(taskTitle)}</h1>
                            <p style="margin:0;font-size:15px;line-height:1.6;color:#374151;">{WebUtilityHtmlEncode(message)}</p>
                            {linkBlock}
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                </table>
              </body>
            </html>
            """;
    }

    private static string WebUtilityHtmlEncode(string value)
    {
        return System.Net.WebUtility.HtmlEncode(value);
    }
}