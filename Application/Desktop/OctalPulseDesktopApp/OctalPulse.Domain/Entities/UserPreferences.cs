namespace OctalPulse.Domain.Entities;

public class UserPreferences
{
    public int Id { get; set; } = 1;
    public string Theme { get; set; } = "Light"; // "Light", "Dark", "System"
    public string AccentColor { get; set; } = "#6366F1"; // Indigo
    public bool IsSidebarCollapsed { get; set; } = false;
    public bool NotificationsEnabled { get; set; } = true;
    public bool AutoReconnectSignalR { get; set; } = true;
}
