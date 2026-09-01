using OctalPulse.Domain.Common;

namespace OctalPulse.Domain.Entities;

public class RefreshToken : AuditableEntity
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;
}