using Microsoft.EntityFrameworkCore;

namespace VeloCenter.Infrastructure.Persistence;

internal sealed class VeloCenterDbContext(DbContextOptions<VeloCenterDbContext> options) : DbContext(options)
{
    public DbSet<ActivityRecord> Activities => Set<ActivityRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var activity = modelBuilder.Entity<ActivityRecord>();

        activity.ToTable("activities");
        activity.HasKey(item => item.Id);
        activity.Property(item => item.Title).HasMaxLength(200).IsRequired();
        activity.Property(item => item.DistanceKm).IsRequired();
        activity.Property(item => item.DurationSeconds).IsRequired();
        activity.Property(item => item.StartTime).IsRequired();
        activity.Property(item => item.Source).IsRequired();
        activity.HasIndex(item => item.StartTime);
    }
}
