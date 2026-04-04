using Microsoft.EntityFrameworkCore;
using KTZH.Models;

namespace KTZH.Data;

/// <summary>
/// EF Core контекст для хранения истории телеметрии в SQLite
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>История телеметрии локомотивов</summary>
    public DbSet<TelemetryHistory> TelemetryHistory => Set<TelemetryHistory>();

    /// <summary>Алерты (пересечения пороговых значений)</summary>
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TelemetryHistory>(entity =>
        {
            entity.HasIndex(e => e.LocomotiveId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.LocomotiveId, e.Timestamp });

            entity.Property(e => e.LocomotiveType)
                  .HasConversion<string>();

            entity.Property(e => e.HealthGrade)
                  .HasConversion<string>();
        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasIndex(e => e.LocomotiveId);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.TriggeredAt);

            entity.Property(e => e.Severity)
                  .HasConversion<string>();
        });
    }
}