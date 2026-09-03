using OctalPulse.Domain.Enums;

namespace OctalPulse.Domain.Entities;

public class CachedMajorTask
{
    public Guid Id { get; set; }
    public Guid TrackId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Details { get; set; }
    public string? Link { get; set; }
    public MajorTaskState State { get; set; }
    public Priority Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public int Order { get; set; }
    public Guid? AssignedUserId { get; set; }
    public int Progress { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
}
