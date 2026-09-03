using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Configurations;

public class TrackMemberConfiguration : IEntityTypeConfiguration<TrackMember>
{
    public void Configure(EntityTypeBuilder<TrackMember> builder)
    {
        builder.ToTable("TrackMembers");

        builder.HasKey(tm => tm.Id);

        builder.HasIndex(tm => new { tm.TrackId, tm.UserId })
            .IsUnique();

        builder.Property(tm => tm.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasQueryFilter(tm => !tm.IsDeleted);

        builder.HasOne(tm => tm.Track)
            .WithMany(t => t.Members)
            .HasForeignKey(tm => tm.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tm => tm.User)
            .WithMany(u => u.TrackMemberships)
            .HasForeignKey(tm => tm.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
