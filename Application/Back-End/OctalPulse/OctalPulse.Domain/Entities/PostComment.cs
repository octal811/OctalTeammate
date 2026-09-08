using OctalPulse.Domain.Common;

namespace OctalPulse.Domain.Entities;

public class PostComment : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsDeleted { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }

    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? ParentCommentId { get; set; }
    public PostComment? ParentComment { get; set; }
    public ICollection<PostComment> Replies { get; set; } = new List<PostComment>();

    public string Content { get; set; } = string.Empty;
}
