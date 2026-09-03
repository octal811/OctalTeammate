using OctalPulse.Domain.Enums;

namespace OctalPulse.Domain.Entities;

public class CachedMinorTask
{
    public Guid Id { get; set; }
    public Guid MajorTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Target { get; set; }
    public MinorTaskState State { get; set; }
    public string? Notes { get; set; }
    public string? Link { get; set; }
    public int Order { get; set; }
    public Guid? AssignedUserId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
}
