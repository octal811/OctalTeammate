using OctalPulse.Domain.Common;

namespace OctalPulse.Domain.Entities;

public class ProjectMember : AuditableEntity
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }

    public Project Project { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<UserProjectRole> Roles { get; set; } = new List<UserProjectRole>();
}
