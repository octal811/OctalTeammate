using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Configurations;

public class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    public void Configure(EntityTypeBuilder<Track> builder)
    {
        builder.ToTable("Tracks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasOne(t => t.Project)
            .WithMany(p => p.Tracks)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.TrackLeadUser)
            .WithMany()
            .HasForeignKey(t => t.TrackLeadUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.MajorTasks)
            .WithOne(mt => mt.Track)
            .HasForeignKey(mt => mt.TrackId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
