namespace OctalPulse.Domain.Entities;

public class SavedAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
}