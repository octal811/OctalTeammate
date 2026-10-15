using Microsoft.Extensions.Logging;
using OctalPulse.Application.Contracts;
using OctalPulse.Application.Services;
using OctalPulse.Infrastructure.Api;

namespace OctalPulse.Infrastructure.Services;

public class AppNotificationService : IAppNotificationService
{
    private readonly ApiClient _apiClient;
    private readonly ILogger<AppNotificationService> _logger;

    private record UnreadCountResult(int UnreadCount);

    public AppNotificationService(ApiClient apiClient, ILogger<AppNotificationService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<PagedNotificationsDto> GetNotificationsAsync(
        int pageNumber = 1,
        int pageSize = 8,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"api/notifications?pageNumber={pageNumber}&pageSize={pageSize}";
            return await _apiClient.SendAsync<PagedNotificationsDto>(
                HttpMethod.Get,
                url,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load notifications (Page={Page}, Size={Size})", pageNumber, pageSize);
            return new PagedNotificationsDto(new List<NotificationItemDto>(), 0, 0, pageNumber, pageSize, false);
        }
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"api/notifications/{notificationId}/read";
            await _apiClient.SendAsync(HttpMethod.Put, url, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mark notification {Id} as read", notificationId);
        }
    }

    public async Task MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _apiClient.SendAsync(HttpMethod.Put, "api/notifications/read-all", null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mark all notifications as read");
        }
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _apiClient.SendAsync<UnreadCountResult>(
                HttpMethod.Get,
                "api/notifications/unread-count",
                null,
                cancellationToken);

            return result?.UnreadCount ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch unread notification count");
            return 0;
        }
    }
}
