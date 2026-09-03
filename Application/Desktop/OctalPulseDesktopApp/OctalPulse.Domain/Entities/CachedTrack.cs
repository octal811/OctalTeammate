namespace OctalPulse.Domain.Entities;

public class CachedTrack
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Progress { get; set; }
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
}
