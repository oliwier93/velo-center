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
}
