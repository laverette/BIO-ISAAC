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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Vulnerability>(entity =>
        {
            entity.HasIndex(e => e.CveId).IsUnique();
            entity.HasIndex(e => e.UrgencyLevel);
            entity.HasIndex(e => e.DateDiscovered);
        });

        modelBuilder.Entity<Trend>(entity =>
        {
            entity.HasIndex(e => new { e.Category, e.Month });
        });
    }
}

