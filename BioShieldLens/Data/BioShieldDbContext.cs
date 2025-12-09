using Microsoft.EntityFrameworkCore;
using BioShieldLens.Models;

namespace BioShieldLens.Data;

public class BioShieldDbContext : DbContext
{
    public BioShieldDbContext(DbContextOptions<BioShieldDbContext> options)
        : base(options)
    {
    }

    public DbSet<Vulnerability> Vulnerabilities { get; set; }
    public DbSet<Trend> Trends { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<AuthUser> AuthUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Vulnerability>(entity =>
        {
            entity.HasIndex(e => e.CveId).IsUnique();
            entity.HasIndex(e => e.UrgencyLevel);
            entity.HasIndex(e => e.DateDiscovered);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<Trend>(entity =>
        {
            entity.HasIndex(e => new { e.Category, e.Month });
        });

        modelBuilder.Entity<AuthUser>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
        });
    }
}

