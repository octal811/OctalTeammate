using Microsoft.AspNetCore.Identity;
using OctalPulse.Domain.Common;
using OctalPulse.Domain.Enums;

namespace OctalPulse.Domain.Entities;

public class User : IdentityUser<Guid>, DomainEntity
{
    public string Name { get; set; } = string.Empty;
    public UserRole MainRole { get; set; }
    public UserRank Rank { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? Bio { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<ProjectMember> ProjectMemberships { get; set; } = new List<ProjectMember>();
    public ICollection<TrackMember> TrackMemberships { get; set; } = new List<TrackMember>();
    public ICollection<Event> CreatedEvents { get; set; } = new List<Event>();
    public ICollection<MajorTask> AssignedMajorTasks { get; set; } = new List<MajorTask>();
    public ICollection<MinorTask> AssignedMinorTasks { get; set; } = new List<MinorTask>();
    public ICollection<Project> CreatedProjects { get; set; } = new List<Project>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<UserBadge> Badges { get; set; } = new List<UserBadge>();
}
