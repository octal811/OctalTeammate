using OctalPulse.Domain.Enums;

namespace OctalPulse.Application.Features.Notification.DTOs;

public record NotificationDto(
    Guid Id,
    Guid UserId,
    Guid? ActorId,
    string? ActorName,
    string? ActorProfilePictureUrl,
    Guid? ProjectId,
    string? ProjectName,
    Guid? TrackId,
    string? TrackName,
    string Title,
    string Message,
    NotificationType Type,
    Guid? EntityId,
    bool IsRead,
    DateTime CreatedDate);

public record PagedNotificationsResponse(
    IReadOnlyList<NotificationDto> Items,
    int TotalCount,
    int UnreadCount,
    int PageNumber,
    int PageSize,
    bool HasMore);
