using OctalPulse.Domain.Common;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Domain.Entities;

public class UserProjectRole : DomainEntity
{
    public Guid ProjectMemberId { get; set; }
    public ProjectRole Role { get; set; }

    public ProjectMember ProjectMember { get; set; } = null!;
}
