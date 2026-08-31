namespace OctalPulse.Domain.Common;

public abstract class DomainEntity
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
}

public abstract class AuditableEntity : DomainEntity
{
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
