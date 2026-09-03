namespace OctalPulse.Application.Interface.Services;

public interface IEmailService
{
    Task SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default);

    Task SendAsync(
        IEnumerable<string> to,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default);
}