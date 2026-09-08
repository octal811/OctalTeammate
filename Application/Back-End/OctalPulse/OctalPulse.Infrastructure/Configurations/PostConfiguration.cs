using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Configurations;

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Content)
            .HasMaxLength(1800)
            .IsRequired();

        builder.Property(p => p.PhotoUrl)
            .HasMaxLength(2048);

        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Project)
            .WithMany()
            .HasForeignKey(p => p.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Track)
            .WithMany()
            .HasForeignKey(p => p.TrackId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Reactions)
            .WithOne(r => r.Post)
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Comments)
            .WithOne(c => c.Post)
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.AuthorId);
        builder.HasIndex(p => p.ProjectId);
        builder.HasIndex(p => p.TrackId);
        builder.HasIndex(p => p.CreatedDate);
    }
}
