using OctalPulse.Domain.Common;

namespace OctalPulse.Domain.Entities;

public class Post : AuditableEntity
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }

    public string Content { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }

    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid? TrackId { get; set; }
    public Track? Track { get; set; }

    public ICollection<PostReaction> Reactions { get; set; } = new List<PostReaction>();
    public ICollection<PostComment> Comments { get; set; } = new List<PostComment>();
}
