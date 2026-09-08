using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Configurations;

public class PostReactionConfiguration : IEntityTypeConfiguration<PostReaction>
{
    public void Configure(EntityTypeBuilder<PostReaction> builder)
    {
        builder.ToTable("PostReactions");

        builder.HasKey(r => r.Id);

        builder.HasOne(r => r.Post)
            .WithMany(p => p.Reactions)
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.PostId, r.UserId })
            .IsUnique();

        // Mirror the Post soft-delete filter so EF doesn't warn about
        // a global query filter on the required end of this relationship.
        builder.HasQueryFilter(r => !r.Post.IsDeleted);
    }
}
