using OctalPulse.Domain.Common;

namespace OctalPulse.Domain.Entities;

public class TrackMember : AuditableEntity
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public Guid TrackId { get; set; }
    public Guid UserId { get; set; }

    public Track Track { get; set; } = null!;
    public User User { get; set; } = null!;
}
