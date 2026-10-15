using Microsoft.EntityFrameworkCore;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Persistence;

namespace OctalPulse.Infrastructure.Repositories;

public class NotificationRepository : BaseRepository<Notification>, INotificationRepository
{
    public NotificationRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<Notification> Items, int TotalCount, int UnreadCount)> GetPagedByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);
        var unreadCount = await query.CountAsync(n => !n.IsRead, cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount, unreadCount);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);

        if (notification != null && !notification.IsRead)
        {
            notification.IsRead = true;
            notification.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ModifiedDate, DateTime.UtcNow),
                cancellationToken);
    }
}
