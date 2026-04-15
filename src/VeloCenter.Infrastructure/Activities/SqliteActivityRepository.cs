using Microsoft.EntityFrameworkCore;
using VeloCenter.Core.Activities;
using VeloCenter.Infrastructure.Persistence;

namespace VeloCenter.Infrastructure.Activities;

public sealed class SqliteActivityRepository(string databasePath) : IActivityRepository
{
    private readonly DbContextOptions<VeloCenterDbContext> _dbContextOptions = VeloCenterSqliteDatabase.CreateOptions(databasePath);

    public IReadOnlyList<ActivitySummary> GetRecentActivities()
    {
        using var dbContext = new VeloCenterDbContext(_dbContextOptions);

        return dbContext.Activities
            .AsNoTracking()
            .ToList()
            .OrderByDescending(activity => activity.StartTime)
            .Select(activity => new ActivitySummary(
                activity.Id,
                activity.Source,
                activity.Title,
                activity.StartTime,
                activity.DistanceKm,
                TimeSpan.FromSeconds(activity.DurationSeconds)))
            .ToList();
    }

    public IReadOnlyList<ActivityRoute> GetActivityRoutes(DateTime? startDate = null, DateTime? endDate = null)
    {
        using var dbContext = new VeloCenterDbContext(_dbContextOptions);

        var activities = dbContext.Activities
            .AsNoTracking()
            .Include(activity => activity.RoutePoints)
            .ToList();

        if (startDate is not null && endDate is not null)
        {
            var normalizedStart = startDate.Value.Date;
            var normalizedEnd = endDate.Value.Date;

            activities = activities
                .Where(activity =>
                {
                    var localDate = activity.StartTime.ToLocalTime().Date;
                    return localDate >= normalizedStart && localDate <= normalizedEnd;
                })
                .ToList();
        }

        return activities
            .Where(activity => activity.RoutePoints.Count > 1)
            .OrderByDescending(activity => activity.StartTime)
            .Select(activity => new ActivityRoute(
                activity.Id,
                activity.Source,
                activity.Title,
                activity.StartTime,
                activity.RoutePoints
                    .OrderBy(point => point.Sequence)
                    .Select(point => new ActivityRoutePoint(point.Latitude, point.Longitude))
                    .ToList()))
            .ToList();
    }
}
