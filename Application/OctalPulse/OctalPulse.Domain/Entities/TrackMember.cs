using OctalPulse.Domain.Common;

namespace OctalPulse.Domain.Entities;

public class TrackMember : AuditableEntity
{
    public Guid TrackId { get; set; }
    public Guid UserId { get; set; }

    public Track Track { get; set; } = null!;
    public User User { get; set; } = null!;
}
