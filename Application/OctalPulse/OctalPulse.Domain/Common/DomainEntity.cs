namespace OctalPulse.Domain.Common;


public interface DomainEntity
{
    
}

public interface CommonEntity : DomainEntity
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
}

public interface AuditableEntity : CommonEntity
{
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
