using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Configurations;

public class UserProjectRoleConfiguration : IEntityTypeConfiguration<UserProjectRole>
{
    public void Configure(EntityTypeBuilder<UserProjectRole> builder)
    {
        builder.ToTable("UserProjectRoles");

        builder.HasKey(upr => upr.Id);

        builder.Property(upr => upr.Role)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasQueryFilter(upr => !upr.IsDeleted);

        builder.HasOne(upr => upr.ProjectMember)
            .WithMany(pm => pm.Roles)
            .HasForeignKey(upr => upr.ProjectMemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
