using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure.Configurations;

namespace OctalPulse.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<UserProjectRole> UserProjectRoles => Set<UserProjectRole>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<TrackMember> TrackMembers => Set<TrackMember>();
    public DbSet<MajorTask> MajorTasks => Set<MajorTask>();
    public DbSet<MinorTask> MinorTasks => Set<MinorTask>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
