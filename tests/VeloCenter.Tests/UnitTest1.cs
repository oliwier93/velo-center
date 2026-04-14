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
            VeloCenter.Infrastructure.Persistence.VeloCenterSqliteDatabase.Initialize(databasePath, seedDemoData: true);
            VeloCenter.Infrastructure.Persistence.VeloCenterSqliteDatabase.Initialize(databasePath, seedDemoData: true);

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

    [Fact]
    public void SqliteRepository_InitializesEmptyDatabaseWithoutSeedData()
    {
        var databaseDirectory = Path.Combine(Path.GetTempPath(), "velo-center-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(databaseDirectory, "velo-center.db");

        try
        {
            VeloCenter.Infrastructure.Persistence.VeloCenterSqliteDatabase.Initialize(databasePath, seedDemoData: false);

            var repository = new VeloCenter.Infrastructure.Activities.SqliteActivityRepository(databasePath);
            var activities = repository.GetRecentActivities();

            Assert.True(File.Exists(databasePath));
            Assert.Empty(activities);
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

    [Fact]
    public void LocalFileImportService_SavesGpxActivityAndUpsertsByFingerprint()
    {
        var databaseDirectory = Path.Combine(Path.GetTempPath(), "velo-center-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(databaseDirectory, "velo-center.db");
        var gpxPath = Path.Combine(databaseDirectory, "morning-ride.gpx");

        try
        {
            Directory.CreateDirectory(databaseDirectory);
            File.WriteAllText(
                gpxPath,
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <gpx version="1.1" creator="velo-center-tests" xmlns="http://www.topografix.com/GPX/1/1">
                  <trk>
                    <name>Morning Ride</name>
                    <trkseg>
                      <trkpt lat="52.2297" lon="21.0122">
                        <time>2026-04-14T06:00:00Z</time>
                      </trkpt>
                      <trkpt lat="52.2307" lon="21.0222">
                        <time>2026-04-14T06:30:00Z</time>
                      </trkpt>
                    </trkseg>
                  </trk>
                </gpx>
                """);

            VeloCenter.Infrastructure.Persistence.VeloCenterSqliteDatabase.Initialize(databasePath, seedDemoData: false);

            var importService = new VeloCenter.Infrastructure.Activities.LocalFileActivityImportService(databasePath);
            var repository = new VeloCenter.Infrastructure.Activities.SqliteActivityRepository(databasePath);

            var firstImport = importService.ImportLocalFile(gpxPath);
            var secondImport = importService.ImportLocalFile(gpxPath);
            var activities = repository.GetRecentActivities();

            Assert.True(firstImport.WasCreated);
            Assert.False(secondImport.WasCreated);
            Assert.Single(activities);
            Assert.Equal(VeloCenter.Core.Activities.ActivitySource.GpxFile, activities[0].Source);
            Assert.Equal("Morning Ride", activities[0].Title);
            Assert.Equal(TimeSpan.FromMinutes(30), activities[0].Duration);
            Assert.True(activities[0].DistanceKm > 0.6);
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

    [Theory]
    [InlineData("Ride", false, null, true)]
    [InlineData("GravelRide", false, null, true)]
    [InlineData("MountainBikeRide", false, null, true)]
    [InlineData("EBikeRide", false, null, true)]
    [InlineData("EMountainBikeRide", false, null, true)]
    [InlineData("Handcycle", false, null, true)]
    [InlineData("Velomobile", false, null, true)]
    [InlineData("VirtualRide", false, null, false)]
    [InlineData("Ride", true, null, false)]
    [InlineData("Run", false, null, false)]
    [InlineData(null, false, "Ride", true)]
    [InlineData(null, false, "VirtualRide", false)]
    public void StravaActivityFilter_OnlyAllowsOutdoorCyclingSportTypes(
        string? sportType,
        bool? trainer,
        string? legacyType,
        bool expected)
    {
        var isAllowed = VeloCenter.Infrastructure.Integrations.Strava.StravaActivityFilter
            .IsOutdoorCyclingActivity(sportType, trainer, legacyType);

        Assert.Equal(expected, isAllowed);
    }

    [Fact]
    public void LocalApplicationResetService_RemovesDatabaseAndStravaFilesAndCreatesCleanDatabase()
    {
        var localAppData = Path.Combine(Path.GetTempPath(), "velo-center-reset-tests", Guid.NewGuid().ToString("N"), "appdata");
        var databasePath = Path.Combine(localAppData, "VeloCenter", "velo-center.db");
        var sessionPath = Path.Combine(localAppData, "VeloCenter", "strava-session.json");
        var configPath = Path.Combine(localAppData, "VeloCenter", "strava-config.json");

        try
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", localAppData);
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            VeloCenter.Infrastructure.Persistence.VeloCenterSqliteDatabase.Initialize(databasePath, seedDemoData: false);
            File.WriteAllText(sessionPath, "{\"token\":\"sample\"}");
            File.WriteAllText(configPath, "{\"clientId\":\"1\"}");

            var importService = new VeloCenter.Infrastructure.Activities.LocalFileActivityImportService(databasePath);
            var repository = new VeloCenter.Infrastructure.Activities.SqliteActivityRepository(databasePath);
            var gpxPath = Path.Combine(localAppData, "sample.gpx");

            File.WriteAllText(
                gpxPath,
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <gpx version="1.1" creator="velo-center-tests" xmlns="http://www.topografix.com/GPX/1/1">
                  <trk>
                    <name>Reset Ride</name>
                    <trkseg>
                      <trkpt lat="52.2297" lon="21.0122">
                        <time>2026-04-14T06:00:00Z</time>
                      </trkpt>
                      <trkpt lat="52.2307" lon="21.0222">
                        <time>2026-04-14T06:30:00Z</time>
                      </trkpt>
                    </trkseg>
                  </trk>
                </gpx>
                """);

            importService.ImportLocalFile(gpxPath);

            var resetService = new VeloCenter.Infrastructure.Maintenance.LocalApplicationResetService(databasePath);
            resetService.ResetAllData();

            var activities = repository.GetRecentActivities();

            Assert.True(File.Exists(databasePath));
            Assert.Empty(activities);
            Assert.False(File.Exists(sessionPath));
            Assert.False(File.Exists(configPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", null);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            if (Directory.Exists(localAppData))
            {
                Directory.Delete(localAppData, recursive: true);
            }
        }
    }
}
