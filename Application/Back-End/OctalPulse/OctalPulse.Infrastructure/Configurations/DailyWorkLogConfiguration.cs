using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Configurations;

public class DailyWorkLogConfiguration : IEntityTypeConfiguration<DailyWorkLog>
{
    public void Configure(EntityTypeBuilder<DailyWorkLog> builder)
    {
        builder.ToTable("DailyWorkLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.WorkDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(l => l.TotalSeconds)
            .IsRequired();

        builder.HasIndex(l => new { l.UserId, l.WorkDate })
            .IsUnique();

        builder.HasQueryFilter(l => !l.IsDeleted);

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}