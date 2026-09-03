using OctalPulse.Domain.Enums;

namespace OctalPulse.Domain.Entities;

public class CachedProject
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Progress { get; set; }
    public ProjectStatus Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public Guid CreatorUserId { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public string CreatorEmail { get; set; } = string.Empty;
    public int MembersCount { get; set; }
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
    public bool IsFavorite { get; set; }
}
