using Microsoft.EntityFrameworkCore;

namespace VeloCenter.Infrastructure.Persistence;

public sealed class VeloCenterDbContext(DbContextOptions<VeloCenterDbContext> options) : DbContext(options)
{
    internal DbSet<ActivityRecord> Activities => Set<ActivityRecord>();
    internal DbSet<ActivityRoutePointRecord> ActivityRoutePoints => Set<ActivityRoutePointRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var activity = modelBuilder.Entity<ActivityRecord>();

        activity.ToTable("activities");
        activity.HasKey(item => item.Id);
        activity.Property(item => item.SourceActivityId).HasMaxLength(128);
        activity.Property(item => item.ImportFingerprint).HasMaxLength(128);
        activity.Property(item => item.Title).HasMaxLength(200).IsRequired();
        activity.Property(item => item.DistanceKm).IsRequired();
        activity.Property(item => item.DurationSeconds).IsRequired();
        activity.Property(item => item.StartTime).IsRequired();
        activity.Property(item => item.Source).IsRequired();
        activity.Property(item => item.ImportedAt);
        activity.Property(item => item.LastUpdatedAt);
        activity.HasIndex(item => item.StartTime);
        activity.HasIndex(item => new { item.Source, item.SourceActivityId })
            .IsUnique()
            .HasFilter("\"SourceActivityId\" IS NOT NULL");
        activity.HasIndex(item => new { item.Source, item.ImportFingerprint })
            .IsUnique()
            .HasFilter("\"ImportFingerprint\" IS NOT NULL");

        var routePoint = modelBuilder.Entity<ActivityRoutePointRecord>();

        routePoint.ToTable("activity_route_points");
        routePoint.HasKey(item => new { item.ActivityId, item.Sequence });
        routePoint.Property(item => item.Latitude).IsRequired();
        routePoint.Property(item => item.Longitude).IsRequired();
        routePoint.HasIndex(item => item.ActivityId);
        routePoint.HasOne(item => item.Activity)
            .WithMany(item => item.RoutePoints)
            .HasForeignKey(item => item.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
