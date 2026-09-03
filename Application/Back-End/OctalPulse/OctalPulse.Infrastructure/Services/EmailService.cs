using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using OctalPulse.Application.Interface.Services;

namespace OctalPulse.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
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
        var message = BuildMessage(to, subject, body, isHtml);

        using var client = new SmtpClient();

        await client.ConnectAsync(_settings.Host, _settings.Port, GetSecureSocketOptions(_settings), cancellationToken);

        if (!string.IsNullOrWhiteSpace(_settings.UserName))
        {
            await client.AuthenticateAsync(_settings.UserName, _settings.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
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
        if (settings.UseSsl)
            return SecureSocketOptions.Auto;

        return settings.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;
    }
}