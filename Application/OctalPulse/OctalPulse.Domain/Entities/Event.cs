using OctalPulse.Domain.Common;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Domain.Entities;

public class Event : AuditableEntity
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EventType Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public bool IsAllDay { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TrackId { get; set; }
    public Guid? MajorTaskId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Project? Project { get; set; }
    public Track? Track { get; set; }
    public MajorTask? MajorTask { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public User? DeletedByUser { get; set; }
}
