using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Configurations;

public class MinorTaskConfiguration : IEntityTypeConfiguration<MinorTask>
{
    public void Configure(EntityTypeBuilder<MinorTask> builder)
    {
        builder.ToTable("MinorTasks");

        builder.HasKey(mn => mn.Id);

        builder.Property(mn => mn.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(mn => mn.Description)
            .HasMaxLength(1000);

        builder.Property(mn => mn.Target)
            .HasMaxLength(1000);

        builder.Property(mn => mn.Notes)
            .HasMaxLength(2000);

        builder.Property(mn => mn.Link)
            .HasMaxLength(500);

        builder.Property(mn => mn.State)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(mn => new { mn.MajorTaskId, mn.Order });

        builder.HasQueryFilter(mn => !mn.IsDeleted);

        builder.HasOne(mn => mn.AssignedUser)
            .WithMany(u => u.AssignedMinorTasks)
            .HasForeignKey(mn => mn.AssignedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(mn => mn.CreatedByUser)
            .WithMany()
            .HasForeignKey(mn => mn.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(mn => mn.DeletedByUser)
            .WithMany()
            .HasForeignKey(mn => mn.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
