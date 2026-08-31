using OctalPulse.Domain.Common;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Domain.Entities;

public class MajorTask : AuditableEntity
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Details { get; set; }
    public int Progress { get; set; }
    public string? Link { get; set; }
    public MajorTaskState State { get; set; }
    public Priority Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public int Order { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid TrackId { get; set; }
    public Guid? AssignedUserId { get; set; }

    public Track Track { get; set; } = null!;
    public User? AssignedUser { get; set; }
    public ICollection<MinorTask> MinorTasks { get; set; } = new List<MinorTask>();
}
