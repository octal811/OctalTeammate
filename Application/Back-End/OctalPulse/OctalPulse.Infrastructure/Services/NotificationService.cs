using Microsoft.Extensions.Options;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private const string EmailVerificationTemplate = "EmailVerification.html";
    private const string PasswordResetTemplate = "PasswordReset.html";
    private const string TaskNotificationTemplate = "TaskNotification.html";

    private static readonly TimeSpan OtpExpiry = TimeSpan.FromMinutes(10);

    private readonly IEmailService _emailService;
    private readonly EmailTemplateRenderer _templateRenderer;
    private readonly EmailSettings _settings;

    public NotificationService(
        IEmailService emailService,
        EmailTemplateRenderer templateRenderer,
        IOptions<EmailSettings> settings)
    {
        _emailService = emailService;
        _templateRenderer = templateRenderer;
        _settings = settings.Value;
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

        var body = _templateRenderer.Render(
            EmailVerificationTemplate,
            new Dictionary<string, string>
            {
                ["AppName"] = _settings.FromName,
                ["Title"] = "Verify your email address",
                ["Message"] = "Welcome to OctalTeammate! Use the code below to verify your email address. The code expires in a few minutes.",
                ["OtpCode"] = otp,
                ["ExpiryMinutes"] = ((int)OtpExpiry.TotalMinutes).ToString(),
                ["SupportEmail"] = _settings.SupportEmail
            });

        return _emailService.SendAsync(to, subject, body, cancellationToken: cancellationToken);
    }

    public Task SendPasswordResetAsync(
        string to,
        string otp,
        CancellationToken cancellationToken = default)
    {
        var subject = "Reset your password";

        var body = _templateRenderer.Render(
            PasswordResetTemplate,
            new Dictionary<string, string>
            {
                ["AppName"] = _settings.FromName,
                ["Title"] = "Password reset requested",
                ["Message"] = "We received a request to reset your password. Use the code below to set a new one. The code expires in a few minutes.",
                ["OtpCode"] = otp,
                ["ExpiryMinutes"] = ((int)OtpExpiry.TotalMinutes).ToString(),
                ["SupportEmail"] = _settings.SupportEmail
            });

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

        var values = new Dictionary<string, string>
        {
            ["AppName"] = _settings.FromName,
            ["TaskTitle"] = taskTitle,
            ["Message"] = message,
            ["SupportEmail"] = _settings.SupportEmail
        };

        if (!string.IsNullOrWhiteSpace(link))
        {
            values["ActionUrl"] = link;
        }

        var body = _templateRenderer.Render(TaskNotificationTemplate, values);

        return _emailService.SendAsync(recipients, subject, body, cancellationToken: cancellationToken);
    }
}