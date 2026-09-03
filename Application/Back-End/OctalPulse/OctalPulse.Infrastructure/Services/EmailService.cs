using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default)
    {
        await SendAsync(new[] { to }, subject, body, isHtml, cancellationToken);
    }

    public async Task SendAsync(
        IEnumerable<string> to,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = BuildMessage(to, subject, body, isHtml);

            using var client = new SmtpClient();

            // Ignore dev certificate validation issues if any
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            var socketOption = GetSecureSocketOptions(_settings);
            _logger.LogInformation("Connecting to SMTP {Host}:{Port} with {SocketOption}...", _settings.Host, _settings.Port, socketOption);

            await client.ConnectAsync(_settings.Host, _settings.Port, socketOption, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_settings.UserName))
            {
                var password = _settings.Password?.Replace(" ", "") ?? string.Empty;
                await client.AuthenticateAsync(_settings.UserName, password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent successfully to {Recipients}", string.Join(", ", to));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipients} via SMTP: {Message}", string.Join(", ", to), ex.Message);
            throw;
        }
    }

    private MimeMessage BuildMessage(IEnumerable<string> recipients, string subject, string body, bool isHtml)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));

        foreach (var recipient in recipients)
        {
            if (!string.IsNullOrWhiteSpace(recipient))
            {
                message.To.Add(MailboxAddress.Parse(recipient));
            }
        }

        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = isHtml ? body : null,
            TextBody = isHtml ? null : body
        };

        message.Body = bodyBuilder.ToMessageBody();

        return message;
    }

    private static SecureSocketOptions GetSecureSocketOptions(EmailSettings settings)
    {
        if (settings.Port == 587)
            return SecureSocketOptions.StartTls;

        if (settings.Port == 465)
            return SecureSocketOptions.SslOnConnect;

        if (settings.UseSsl)
            return SecureSocketOptions.Auto;

        return SecureSocketOptions.StartTlsWhenAvailable;
    }
}