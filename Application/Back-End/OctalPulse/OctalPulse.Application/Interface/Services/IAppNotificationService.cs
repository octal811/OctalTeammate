using OctalPulse.Application.Features.Notification.DTOs;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Interface.Services;

public interface IAppNotificationService
{
    Task CreateAndSendNotificationAsync(
        Guid recipientUserId,
        Guid? actorUserId,
        Guid? projectId,
        Guid? trackId,
        string title,
        string message,
        NotificationType type,
        Guid? entityId,
        CancellationToken cancellationToken = default);

    Task NotifyTrackMembersAsync(
        Guid trackId,
        Guid actorUserId,
        string title,
        string message,
        NotificationType type,
        Guid? entityId,
        CancellationToken cancellationToken = default);

    Task NotifyProjectMembersAsync(
        Guid projectId,
        Guid actorUserId,
        string title,
        string message,
        NotificationType type,
        Guid? entityId,
        CancellationToken cancellationToken = default);

    Task NotifyWorkspaceUsersAsync(
        Guid actorUserId,
        string title,
        string message,
        NotificationType type,
        Guid? entityId,
        CancellationToken cancellationToken = default);

    Task<PagedNotificationsResponse> GetNotificationsAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
