using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(n => n.Message)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(n => n.ActorName)
            .HasMaxLength(200);

        builder.Property(n => n.ActorProfilePictureUrl)
            .HasMaxLength(500);

        builder.Property(n => n.ProjectName)
            .HasMaxLength(200);

        builder.Property(n => n.TrackName)
            .HasMaxLength(200);

        builder.Property(n => n.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(n => n.IsRead)
            .HasDefaultValue(false);

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedDate });
        builder.HasIndex(n => n.UserId);

        builder.HasQueryFilter(n => !n.IsDeleted);

        builder.HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.Actor)
            .WithMany()
            .HasForeignKey(n => n.ActorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Project)
            .WithMany()
            .HasForeignKey(n => n.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Track)
            .WithMany()
            .HasForeignKey(n => n.TrackId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
