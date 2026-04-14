namespace VeloCenter.Tests;

public sealed class UnitTest1
{
    [Fact]
    public void TrainingOverview_ComputesActivityCountDistanceAndDuration()
    {
        VeloCenter.Core.Activities.ActivitySummary[] activities =
        [
            new(
                Guid.NewGuid(),
                VeloCenter.Core.Activities.ActivitySource.FitFile,
                "Intervals",
                new DateTimeOffset(2026, 4, 11, 7, 0, 0, TimeSpan.Zero),
                40.5,
                TimeSpan.FromMinutes(90)),
            new(
                Guid.NewGuid(),
                VeloCenter.Core.Activities.ActivitySource.Strava,
                "Long ride",
                new DateTimeOffset(2026, 4, 12, 8, 0, 0, TimeSpan.Zero),
                81.3,
                TimeSpan.FromMinutes(180)),
        ];

        var overview = VeloCenter.Core.Activities.TrainingOverview.FromActivities(activities);

        Assert.Equal(2, overview.TotalActivities);
        Assert.Equal(121.8, overview.TotalDistanceKm, 1);
        Assert.Equal(TimeSpan.FromMinutes(270), overview.TotalDuration);
    }

    [Fact]
    public void SqliteRepository_InitializesDatabaseAndReturnsSeededActivities()
    {
        var databaseDirectory = Path.Combine(Path.GetTempPath(), "velo-center-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(databaseDirectory, "velo-center.db");

        try
        {
            VeloCenter.Infrastructure.Persistence.VeloCenterSqliteDatabase.Initialize(databasePath);
            VeloCenter.Infrastructure.Persistence.VeloCenterSqliteDatabase.Initialize(databasePath);

            var repository = new VeloCenter.Infrastructure.Activities.SqliteActivityRepository(databasePath);
            var activities = repository.GetRecentActivities();
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";";
            var migrationCount = Convert.ToInt32(command.ExecuteScalar());

            Assert.True(File.Exists(databasePath));
            Assert.Equal(2, migrationCount);
            Assert.Equal(3, activities.Count);
            Assert.Equal("Sweet spot ride", activities[0].Title);
            Assert.Equal("Endurance spin", activities[1].Title);
            Assert.Equal("Recovery ride", activities[2].Title);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            if (Directory.Exists(databaseDirectory))
            {
                Directory.Delete(databaseDirectory, recursive: true);
            }
        }
    }
}
