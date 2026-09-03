using OctalPulse.Domain.Enums;

namespace OctalPulse.Domain.Entities;

public class LocalSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public UserRole MainRole { get; set; }
    public UserRank Rank { get; set; }
    public bool RememberMe { get; set; }
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
