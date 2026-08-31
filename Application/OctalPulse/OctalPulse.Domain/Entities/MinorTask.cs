using OctalPulse.Domain.Common;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Domain.Entities;

public class MinorTask : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Target { get; set; }
    public MinorTaskState State { get; set; }
    public string? Notes { get; set; }
    public string? Link { get; set; }
    public int Order { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid MajorTaskId { get; set; }
    public Guid? AssignedUserId { get; set; }

    public MajorTask MajorTask { get; set; } = null!;
    public User? AssignedUser { get; set; }
}
