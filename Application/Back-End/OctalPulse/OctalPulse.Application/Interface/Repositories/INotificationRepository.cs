using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Interface.Repositories;

public interface INotificationRepository : IGenericRepository<Notification>
{
    Task<(IReadOnlyList<Notification> Items, int TotalCount, int UnreadCount)> GetPagedByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
}
