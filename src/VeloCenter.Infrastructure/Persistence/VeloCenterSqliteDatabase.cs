using Microsoft.EntityFrameworkCore;
using VeloCenter.Core.Activities;

namespace VeloCenter.Infrastructure.Persistence;

public static class VeloCenterSqliteDatabase
{
    public static string GetDefaultDatabasePath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("VELOCENTER_DB_PATH");

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return EnsureDatabaseDirectory(configuredPath);
        }

        try
        {
            var localApplicationDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VeloCenter",
                "velo-center.db");

            return EnsureDatabaseDirectory(localApplicationDataPath);
        }
        catch (UnauthorizedAccessException)
        {
            var tempDatabasePath = Path.Combine(
                Path.GetTempPath(),
                "VeloCenter",
                "velo-center.db");

            return EnsureDatabaseDirectory(tempDatabasePath);
        }
    }

    public static void Initialize(string databasePath)
    {
        var dbContextOptions = CreateOptions(databasePath);

        using var dbContext = new VeloCenterDbContext(dbContextOptions);

        dbContext.Database.EnsureCreated();

        if (dbContext.Activities.Any())
        {
            return;
        }

        dbContext.Activities.AddRange(CreateSeedActivities());
        dbContext.SaveChanges();
    }

    internal static DbContextOptions<VeloCenterDbContext> CreateOptions(string databasePath)
    {
        databasePath = EnsureDatabaseDirectory(databasePath);

        return new DbContextOptionsBuilder<VeloCenterDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
    }

    private static string EnsureDatabaseDirectory(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var databaseDirectory = Path.GetDirectoryName(databasePath);

        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        return databasePath;
    }

    private static IEnumerable<ActivityRecord> CreateSeedActivities()
    {
        var today = DateTimeOffset.Now.Date;

        return
        [
            new ActivityRecord
            {
                Id = Guid.Parse("2ec213f8-6c5d-4af1-8821-7df59c2ad8f1"),
                Source = ActivitySource.FitFile,
                Title = "Sweet spot ride",
                StartTime = today.AddDays(-1).AddHours(6),
                DistanceKm = 48.6,
                DurationSeconds = (int)TimeSpan.FromMinutes(101).TotalSeconds,
            },
            new ActivityRecord
            {
                Id = Guid.Parse("0ea6ec0d-46f8-4ce4-97da-769c9ba93c7d"),
                Source = ActivitySource.Strava,
                Title = "Endurance spin",
                StartTime = today.AddDays(-3).AddHours(7),
                DistanceKm = 62.2,
                DurationSeconds = (int)TimeSpan.FromMinutes(134).TotalSeconds,
            },
            new ActivityRecord
            {
                Id = Guid.Parse("d6c4e8a6-56ab-4f7f-8a8f-7243d33df11d"),
                Source = ActivitySource.GpxFile,
                Title = "Recovery ride",
                StartTime = today.AddDays(-5).AddHours(18),
                DistanceKm = 24.8,
                DurationSeconds = (int)TimeSpan.FromMinutes(55).TotalSeconds,
            },
        ];
    }
}
