using CommunityToolkit.Mvvm.ComponentModel;
using OctalPulse.Application.Contracts;

namespace OctalPulse.ViewModels;

public partial class NotificationItemViewModel : ObservableObject
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public Guid? ActorId { get; }
    public string ActorName { get; }
    public string? ActorProfilePictureUrl { get; }
    public Guid? ProjectId { get; }
    public string? ProjectName { get; }
    public Guid? TrackId { get; }
    public string? TrackName { get; }
    public string Title { get; }
    public string Message { get; }
    public string Type { get; }
    public Guid? EntityId { get; }
    public DateTime CreatedDate { get; }

    [ObservableProperty]
    private bool _isRead;

    public string ActorInitial => string.IsNullOrWhiteSpace(ActorName)
        ? "O"
        : ActorName.Trim()[0].ToString().ToUpperInvariant();

    public bool HasProject => !string.IsNullOrWhiteSpace(ProjectName);
    public bool HasTrack => !string.IsNullOrWhiteSpace(TrackName);

    public string TimeAgo => CalculateTimeAgo(CreatedDate);

    public string TypeBadgeColor => Type?.ToLowerInvariant() switch
    {
        "majortask" or "minortask" or "task" => "#2563EB",   // Blue
        "event" => "#7C3AED",                                 // Violet
        "post" => "#0284C7",                                  // Sky
        "comment" => "#D97706",                               // Amber
        "project" or "track" => "#059669",                    // Emerald
        _ => "#4F46E5"                                        // Indigo
    };

    public string TypeIconGlyph => Type?.ToLowerInvariant() switch
    {
        "majortask" => "\uE7C3",      // Checkmark / Task
        "minortask" => "\uE823",      // Sub-bullet
        "event" => "\uE787",          // Calendar
        "post" => "\uE715",           // Send / Post
        "comment" => "\uE90A",        // Chat bubble
        "project" => "\uE8F1",        // Folder
        "track" => "\uE8B7",          // View all
        _ => "\uEA8F"                 // Bell
    };

    public NotificationItemViewModel(NotificationItemDto dto)
    {
        Id = dto.Id;
        UserId = dto.UserId;
        ActorId = dto.ActorId;
        ActorName = !string.IsNullOrWhiteSpace(dto.ActorName) ? dto.ActorName : "Team Member";
        ActorProfilePictureUrl = dto.ActorProfilePictureUrl;
        ProjectId = dto.ProjectId;
        ProjectName = dto.ProjectName;
        TrackId = dto.TrackId;
        TrackName = dto.TrackName;
        Title = dto.Title;
        Message = dto.Message;
        Type = dto.Type;
        EntityId = dto.EntityId;
        CreatedDate = dto.CreatedDate;
        _isRead = dto.IsRead;
    }

    private static string CalculateTimeAgo(DateTime dt)
    {
        var localDt = dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
        var diff = DateTime.Now - localDt;

        if (diff.TotalSeconds < 60) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return localDt.ToString("MMM dd");
    }
}
