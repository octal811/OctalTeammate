using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Configurations;

public class PostCommentConfiguration : IEntityTypeConfiguration<PostComment>
{
    public void Configure(EntityTypeBuilder<PostComment> builder)
    {
        builder.ToTable("PostComments");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content)
            .HasMaxLength(800)
            .IsRequired();

        // Combine comment's own soft-delete with Post's soft-delete filter
        // to avoid EF Core warning about mismatched query filters on required navigations.
        builder.HasQueryFilter(c => !c.IsDeleted && !c.Post.IsDeleted);

        builder.HasOne(c => c.Post)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ParentComment)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.PostId);
        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.ParentCommentId);
        builder.HasIndex(c => c.CreatedDate);
    }
}
