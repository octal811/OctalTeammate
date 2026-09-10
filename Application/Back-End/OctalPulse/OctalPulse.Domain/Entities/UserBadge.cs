using OctalPulse.Domain.Common;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Domain.Entities;

public class UserBadge : AuditableEntity
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public Guid UserId { get; set; }
    public BadgeType Type { get; set; }
    public BadgeLevel Level { get; set; }
    public DateTime AwardedDate { get; set; }
    public Guid? RefId { get; set; }

    public User User { get; set; } = null!;
}