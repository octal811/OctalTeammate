using OctalPulse.Domain.Common;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Domain.Entities;

public class PostReaction : DomainEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ReactionType Type { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
