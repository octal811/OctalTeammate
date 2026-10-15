using OctalPulse.Domain.Common;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Domain.Entities;

public class Notification : AuditableEntity
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public Guid UserId { get; set; }
    public Guid? ActorId { get; set; }
    public string? ActorName { get; set; }
    public string? ActorProfilePictureUrl { get; set; }

    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public Guid? TrackId { get; set; }
    public string? TrackName { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.General;
    public Guid? EntityId { get; set; }

    public bool IsRead { get; set; }

    public User User { get; set; } = null!;
    public User? Actor { get; set; }
    public Project? Project { get; set; }
    public Track? Track { get; set; }
}
