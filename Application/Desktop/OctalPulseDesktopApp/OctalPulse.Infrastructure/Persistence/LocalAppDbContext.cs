using System.IO;
using Microsoft.EntityFrameworkCore;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Infrastructure.Persistence;

public class LocalAppDbContext : DbContext
{
    public DbSet<LocalSession> Sessions => Set<LocalSession>();
    public DbSet<UserPreferences> Preferences => Set<UserPreferences>();
    public DbSet<GitHubIntegrationSettings> GitHubSettings => Set<GitHubIntegrationSettings>();
    public DbSet<SavedAccount> SavedAccounts => Set<SavedAccount>();

    public LocalAppDbContext()
    {
    }

    public LocalAppDbContext(DbContextOptions<LocalAppDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dbDir = Path.Combine(appData, "OctalPulse");
            if (!Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }
            var dbPath = Path.Combine(dbDir, "octal_desktop.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LocalSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired();
        });

        modelBuilder.Entity<UserPreferences>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<GitHubIntegrationSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<SavedAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
        });
    }
}
