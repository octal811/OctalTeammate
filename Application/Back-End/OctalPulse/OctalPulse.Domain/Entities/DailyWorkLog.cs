using OctalPulse.Domain.Common;

namespace OctalPulse.Domain.Entities;

public class DailyWorkLog : AuditableEntity
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public Guid UserId { get; set; }
    public DateOnly WorkDate { get; set; }
    public long TotalSeconds { get; set; }
    public DateTime UpdatedDate { get; set; }

    public User User { get; set; } = null!;
}