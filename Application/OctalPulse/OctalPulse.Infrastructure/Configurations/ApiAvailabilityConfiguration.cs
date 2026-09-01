using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Configurations;

public class ApiAvailabilityConfiguration : IEntityTypeConfiguration<ApiAvailability>
{
    public void Configure(EntityTypeBuilder<ApiAvailability> builder)
    {
        builder.ToTable("ApiAvailability");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Key)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(a => a.Key)
            .IsUnique();

        builder.Property(a => a.IsEnabled)
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .IsRequired();

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}