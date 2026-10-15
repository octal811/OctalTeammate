namespace OctalPulse.Application.Contracts;

public record NotificationItemDto(
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
    string Type,
    Guid? EntityId,
    bool IsRead,
    DateTime CreatedDate);

public record PagedNotificationsDto(
    List<NotificationItemDto> Items,
    int TotalCount,
    int UnreadCount,
    int PageNumber,
    int PageSize,
    bool HasMore);
