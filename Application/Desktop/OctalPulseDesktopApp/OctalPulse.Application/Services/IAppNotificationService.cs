using OctalPulse.Application.Contracts;

namespace OctalPulse.Application.Services;

public interface IAppNotificationService
{
    Task<PagedNotificationsDto> GetNotificationsAsync(int pageNumber = 1, int pageSize = 8, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);
}
