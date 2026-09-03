using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Configurations;

public class MajorTaskConfiguration : IEntityTypeConfiguration<MajorTask>
{
    public void Configure(EntityTypeBuilder<MajorTask> builder)
    {
        builder.ToTable("MajorTasks");

        builder.HasKey(mt => mt.Id);

        builder.Property(mt => mt.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(mt => mt.Description)
            .HasMaxLength(1000);

        builder.Property(mt => mt.Details)
            .HasMaxLength(4000);

        builder.Property(mt => mt.Link)
            .HasMaxLength(500);

        builder.Property(mt => mt.State)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(mt => mt.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(mt => mt.Progress)
            .HasDefaultValue(0);

        builder.HasIndex(mt => new { mt.TrackId, mt.Order });

        builder.HasQueryFilter(mt => !mt.IsDeleted);

        builder.HasOne(mt => mt.AssignedUser)
            .WithMany(u => u.AssignedMajorTasks)
            .HasForeignKey(mt => mt.AssignedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(mt => mt.CreatedByUser)
            .WithMany()
            .HasForeignKey(mt => mt.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(mt => mt.DeletedByUser)
            .WithMany()
            .HasForeignKey(mt => mt.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(mt => mt.MinorTasks)
            .WithOne(mn => mn.MajorTask)
            .HasForeignKey(mn => mn.MajorTaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
