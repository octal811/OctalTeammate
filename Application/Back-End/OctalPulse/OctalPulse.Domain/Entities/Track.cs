using OctalPulse.Domain.Common;

namespace OctalPulse.Domain.Entities;

public class Track : AuditableEntity
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? TrackLeadUserId { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Project Project { get; set; } = null!;
    public User? TrackLeadUser { get; set; }
    public User? DeletedByUser { get; set; }
    public ICollection<TrackMember> Members { get; set; } = new List<TrackMember>();
    public ICollection<MajorTask> MajorTasks { get; set; } = new List<MajorTask>();
}
