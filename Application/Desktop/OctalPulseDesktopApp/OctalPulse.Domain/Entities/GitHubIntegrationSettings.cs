namespace OctalPulse.Domain.Entities;

public class GitHubIntegrationSettings
{
    public int Id { get; set; } = 1;
    public bool IsConnected { get; set; }
    public string? GitHubUsername { get; set; }
    public string? AvatarUrl { get; set; }
    public string? DefaultRepository { get; set; }
    public DateTime? ConnectedAt { get; set; }
}
