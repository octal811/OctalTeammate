using Microsoft.Extensions.Logging;
using OctalPulse.Application.Features.Notification.DTOs;
using OctalPulse.Application.Interface.Repositories;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Infrastructure.Services;

public class AppNotificationService : IAppNotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly ILogger<AppNotificationService> _logger;

    public AppNotificationService(
        IUnitOfWork unitOfWork,
        IRealtimeNotifier realtimeNotifier,
        ILogger<AppNotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
        _logger = logger;
    }

    public async Task CreateAndSendNotificationAsync(
        Guid recipientUserId,
        Guid? actorUserId,
        Guid? projectId,
        Guid? trackId,
        string title,
        string message,
        NotificationType type,
        Guid? entityId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (actorName, actorPic) = await GetActorInfoAsync(actorUserId, cancellationToken);
            var (projName, trkName) = await GetProjectAndTrackNameAsync(projectId, trackId, cancellationToken);

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = recipientUserId,
                ActorId = actorUserId,
                ActorName = actorName,
                ActorProfilePictureUrl = actorPic,
                ProjectId = projectId,
                ProjectName = projName,
                TrackId = trackId,
                TrackName = trkName,
                Title = title,
                Message = message,
                Type = type,
                EntityId = entityId,
                IsRead = false,
                CreatedDate = DateTime.UtcNow,
                IsDeleted = false
            };

            await _unitOfWork.Notifications.AddAsync(notification, cancellationToken);
            await _unitOfWork.CompleteAsync(cancellationToken);

            var dto = MapToDto(notification);
            await _realtimeNotifier.SendNotificationAsync(recipientUserId, dto, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create and send notification to user {UserId}", recipientUserId);
        }
    }

    public async Task NotifyTrackMembersAsync(
        Guid trackId,
        Guid actorUserId,
        string title,
        string message,
        NotificationType type,
        Guid? entityId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var track = await _unitOfWork.Tracks.GetByIdAsync(trackId, cancellationToken);
            if (track == null) return;

            var projectId = track.ProjectId;
            var (projName, trkName) = await GetProjectAndTrackNameAsync(projectId, trackId, cancellationToken);
            var (actorName, actorPic) = await GetActorInfoAsync(actorUserId, cancellationToken);

            var recipientUserIds = new HashSet<Guid>();

            // 1. Explicit track members
            var trackMembers = await _unitOfWork.TrackMembers.FindAsync(
                tm => tm.TrackId == trackId && tm.Status == MembershipStatus.Approved && tm.UserId != actorUserId,
                cancellationToken);
            foreach (var tm in trackMembers)
            {
                recipientUserIds.Add(tm.UserId);
            }

            // 2. Track lead
            if (track.TrackLeadUserId.HasValue && track.TrackLeadUserId.Value != Guid.Empty &&
                track.TrackLeadUserId.Value != actorUserId)
            {
                recipientUserIds.Add(track.TrackLeadUserId.Value);
            }

            // 3. Approved project members of this track's project
            var projectMembers = await _unitOfWork.ProjectMembers.FindAsync(
                pm => pm.ProjectId == projectId && pm.Status == MembershipStatus.Approved && pm.UserId != actorUserId,
                cancellationToken);
            foreach (var pm in projectMembers)
            {
                recipientUserIds.Add(pm.UserId);
            }

            // 4. Project creator
            var project = await _unitOfWork.Projects.GetByIdAsync(projectId, cancellationToken);
            if (project != null && project.CreatedByUserId != Guid.Empty && project.CreatedByUserId != actorUserId)
            {
                recipientUserIds.Add(project.CreatedByUserId);
            }

            if (!recipientUserIds.Any()) return;

            var now = DateTime.UtcNow;
            var notifications = new List<Notification>();

            foreach (var recipientId in recipientUserIds)
            {
                notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = recipientId,
                    ActorId = actorUserId,
                    ActorName = actorName,
                    ActorProfilePictureUrl = actorPic,
                    ProjectId = projectId,
                    ProjectName = projName,
                    TrackId = trackId,
                    TrackName = trkName,
                    Title = title,
                    Message = message,
                    Type = type,
                    EntityId = entityId,
                    IsRead = false,
                    CreatedDate = now,
                    IsDeleted = false
                });
            }

            await _unitOfWork.Notifications.AddRangeAsync(notifications, cancellationToken);
            await _unitOfWork.CompleteAsync(cancellationToken);

            foreach (var n in notifications)
            {
                var dto = MapToDto(n);
                await _realtimeNotifier.SendNotificationAsync(n.UserId, dto, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify track members for track {TrackId}", trackId);
        }
    }

    public async Task NotifyProjectMembersAsync(
        Guid projectId,
        Guid actorUserId,
        string title,
        string message,
        NotificationType type,
        Guid? entityId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var project = await _unitOfWork.Projects.GetByIdAsync(projectId, cancellationToken);
            if (project == null) return;

            var projName = project.Title;
            var (actorName, actorPic) = await GetActorInfoAsync(actorUserId, cancellationToken);

            var projectMembers = await _unitOfWork.ProjectMembers.FindAsync(
                pm => pm.ProjectId == projectId && pm.Status == MembershipStatus.Approved && pm.UserId != actorUserId,
                cancellationToken);

            var recipientUserIds = projectMembers.Select(pm => pm.UserId).Distinct().ToList();
            if (project.CreatedByUserId != Guid.Empty && project.CreatedByUserId != actorUserId &&
                !recipientUserIds.Contains(project.CreatedByUserId))
            {
                recipientUserIds.Add(project.CreatedByUserId);
            }

            if (!recipientUserIds.Any()) return;

            var now = DateTime.UtcNow;
            var notifications = new List<Notification>();

            foreach (var recipientId in recipientUserIds)
            {
                notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = recipientId,
                    ActorId = actorUserId,
                    ActorName = actorName,
                    ActorProfilePictureUrl = actorPic,
                    ProjectId = projectId,
                    ProjectName = projName,
                    TrackId = null,
                    TrackName = null,
                    Title = title,
                    Message = message,
                    Type = type,
                    EntityId = entityId,
                    IsRead = false,
                    CreatedDate = now,
                    IsDeleted = false
                });
            }

            await _unitOfWork.Notifications.AddRangeAsync(notifications, cancellationToken);
            await _unitOfWork.CompleteAsync(cancellationToken);

            foreach (var n in notifications)
            {
                var dto = MapToDto(n);
                await _realtimeNotifier.SendNotificationAsync(n.UserId, dto, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify project members for project {ProjectId}", projectId);
        }
    }

    public async Task NotifyWorkspaceUsersAsync(
        Guid actorUserId,
        string title,
        string message,
        NotificationType type,
        Guid? entityId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (actorName, actorPic) = await GetActorInfoAsync(actorUserId, cancellationToken);

            var activeUsers = await _unitOfWork.Users.FindAsync(
                u => !u.IsDeleted && u.Id != actorUserId,
                cancellationToken);

            var recipientUserIds = activeUsers.Select(u => u.Id).Distinct().ToList();
            if (!recipientUserIds.Any()) return;

            var now = DateTime.UtcNow;
            var notifications = new List<Notification>();

            foreach (var recipientId in recipientUserIds)
            {
                notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = recipientId,
                    ActorId = actorUserId,
                    ActorName = actorName,
                    ActorProfilePictureUrl = actorPic,
                    ProjectId = null,
                    ProjectName = null,
                    TrackId = null,
                    TrackName = null,
                    Title = title,
                    Message = message,
                    Type = type,
                    EntityId = entityId,
                    IsRead = false,
                    CreatedDate = now,
                    IsDeleted = false
                });
            }

            await _unitOfWork.Notifications.AddRangeAsync(notifications, cancellationToken);
            await _unitOfWork.CompleteAsync(cancellationToken);

            foreach (var n in notifications)
            {
                var dto = MapToDto(n);
                await _realtimeNotifier.SendNotificationAsync(n.UserId, dto, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify workspace users for actor {ActorUserId}", actorUserId);
        }
    }

    public async Task<PagedNotificationsResponse> GetNotificationsAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount, unreadCount) = await _unitOfWork.Notifications.GetPagedByUserIdAsync(
            userId,
            pageNumber,
            pageSize,
            cancellationToken);

        var dtos = items.Select(MapToDto).ToList();
        var hasMore = (pageNumber * pageSize) < totalCount;

        return new PagedNotificationsResponse(
            dtos,
            totalCount,
            unreadCount,
            pageNumber,
            pageSize,
            hasMore);
    }

    public async Task MarkAsReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.Notifications.MarkAsReadAsync(notificationId, userId, cancellationToken);
    }

    public async Task MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.Notifications.MarkAllAsReadAsync(userId, cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Notifications.GetUnreadCountAsync(userId, cancellationToken);
    }

    private async Task<(string? Name, string? PictureUrl)> GetActorInfoAsync(Guid? actorUserId, CancellationToken cancellationToken)
    {
        if (!actorUserId.HasValue || actorUserId.Value == Guid.Empty)
            return (null, null);

        var actor = await _unitOfWork.Users.GetByIdAsync(actorUserId.Value, cancellationToken);
        return (actor?.Name, actor?.ProfilePictureUrl);
    }

    private async Task<(string? ProjectName, string? TrackName)> GetProjectAndTrackNameAsync(
        Guid? projectId,
        Guid? trackId,
        CancellationToken cancellationToken)
    {
        string? projName = null;
        string? trkName = null;

        if (projectId.HasValue && projectId.Value != Guid.Empty)
        {
            var project = await _unitOfWork.Projects.GetByIdAsync(projectId.Value, cancellationToken);
            projName = project?.Title;
        }

        if (trackId.HasValue && trackId.Value != Guid.Empty)
        {
            var track = await _unitOfWork.Tracks.GetByIdAsync(trackId.Value, cancellationToken);
            trkName = track?.Name;
            if (string.IsNullOrEmpty(projName) && track?.ProjectId != null)
            {
                var project = await _unitOfWork.Projects.GetByIdAsync(track.ProjectId, cancellationToken);
                projName = project?.Title;
            }
        }

        return (projName, trkName);
    }

    private static NotificationDto MapToDto(Notification n) => new(
        n.Id,
        n.UserId,
        n.ActorId,
        n.ActorName,
        n.ActorProfilePictureUrl,
        n.ProjectId,
        n.ProjectName,
        n.TrackId,
        n.TrackName,
        n.Title,
        n.Message,
        n.Type,
        n.EntityId,
        n.IsRead,
        n.CreatedDate);
}
