using OctalPulse.Domain.Common;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Domain.Entities;

public class MinorTask : AuditableEntity
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Target { get; set; }
    public MinorTaskState State { get; set; }
    public string? Notes { get; set; }
    public string? Link { get; set; }
    public int Order { get; set; }
    public TimeSpan? WorkTime { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid MajorTaskId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public MajorTask MajorTask { get; set; } = null!;
    public User? AssignedUser { get; set; }
    public User? CreatedByUser { get; set; }
    public User? DeletedByUser { get; set; }
}
