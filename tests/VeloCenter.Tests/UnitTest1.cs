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

    [Fact]
    public void ProgressViewModel_BuildsYearlyCumulativeDistanceSeries()
    {
        var localOffset2026 = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 3, 1, 12, 0, 0));
        var localOffset2025 = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2025, 3, 1, 12, 0, 0));

        VeloCenter.Core.Activities.ActivitySummary[] activities =
        [
            new(
                Guid.NewGuid(),
                VeloCenter.Core.Activities.ActivitySource.GpxFile,
                "Ride 2026-03-01",
                new DateTimeOffset(2026, 3, 1, 12, 0, 0, localOffset2026),
                50,
                TimeSpan.FromMinutes(100)),
            new(
                Guid.NewGuid(),
                VeloCenter.Core.Activities.ActivitySource.Strava,
                "Ride 2026-03-02",
                new DateTimeOffset(2026, 3, 2, 12, 0, 0, localOffset2026),
                100,
                TimeSpan.FromMinutes(180)),
            new(
                Guid.NewGuid(),
                VeloCenter.Core.Activities.ActivitySource.FitFile,
                "Ride 2025-03-01",
                new DateTimeOffset(2025, 3, 1, 12, 0, 0, localOffset2025),
                20,
                TimeSpan.FromMinutes(60)),
        ];

        var viewModel = new VeloCenter.App.ViewModels.ProgressViewModel(activities);
        var series2026 = Assert.Single(viewModel.AnnualSeries, series => series.Year == 2026);
        var marchFirstPoint = Assert.Single(series2026.Points, point => point.DayOfYear == 60);
        var marchSecondPoint = Assert.Single(series2026.Points, point => point.DayOfYear == 61);

        Assert.Equal(2, viewModel.AnnualSeries.Count);
        Assert.Equal(150, series2026.TotalDistanceKm, 3);
        Assert.Equal(50, marchFirstPoint.CumulativeDistanceKm, 3);
        Assert.Equal(150, marchSecondPoint.CumulativeDistanceKm, 3);
    }

    [Fact]
    public void ProgressViewModel_BuildsAtLeastSixDistanceTicksWithNiceRoundedScale()
    {
        var localOffset2026 = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 7, 15, 12, 0, 0));
        VeloCenter.Core.Activities.ActivitySummary[] activities =
        [
            new(
                Guid.NewGuid(),
                VeloCenter.Core.Activities.ActivitySource.Strava,
                "Season total",
                new DateTimeOffset(2026, 7, 15, 12, 0, 0, localOffset2026),
                1750,
                TimeSpan.FromHours(50)),
        ];

        var viewModel = new VeloCenter.App.ViewModels.ProgressViewModel(activities);

        Assert.Equal(
            ["2000 km", "1600 km", "1200 km", "800 km", "400 km", "0 km"],
            viewModel.DistanceTicks.Select(tick => tick.Label).ToArray());
    }

    [Fact]
    public void ProgressViewModel_FiltersVisibleSeriesAndKeepsNewestOnTop()
    {
        var localOffset2026 = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 3, 1, 12, 0, 0));
        var localOffset2025 = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2025, 3, 1, 12, 0, 0));

        VeloCenter.Core.Activities.ActivitySummary[] activities =
        [
            new(
                Guid.NewGuid(),
                VeloCenter.Core.Activities.ActivitySource.GpxFile,
                "Ride 2026-03-01",
                new DateTimeOffset(2026, 3, 1, 12, 0, 0, localOffset2026),
                50,
                TimeSpan.FromMinutes(100)),
            new(
                Guid.NewGuid(),
                VeloCenter.Core.Activities.ActivitySource.FitFile,
                "Ride 2025-03-01",
                new DateTimeOffset(2025, 3, 1, 12, 0, 0, localOffset2025),
                20,
                TimeSpan.FromMinutes(60)),
        ];

        var viewModel = new VeloCenter.App.ViewModels.ProgressViewModel(activities);
        var year2025 = Assert.Single(viewModel.AnnualSeries, series => series.Year == 2025);

        Assert.Equal([2025, 2026], viewModel.VisibleAnnualSeries.Select(series => series.Year).ToArray());

        year2025.IsVisible = false;

        Assert.Single(viewModel.VisibleAnnualSeries);
        Assert.Equal(2026, viewModel.VisibleAnnualSeries[0].Year);
    }

    [Fact]
    public void ProgressViewModel_CurrentYearSeriesEndsAtTodayInsteadOfYearEnd()
    {
        var today = DateTime.Today;
        var localOffset = TimeZoneInfo.Local.GetUtcOffset(today.AddHours(12));
        VeloCenter.Core.Activities.ActivitySummary[] activities =
        [
            new(
                Guid.NewGuid(),
                VeloCenter.Core.Activities.ActivitySource.Strava,
                "Current year ride",
                new DateTimeOffset(today.Year, 1, 1, 12, 0, 0, localOffset),
                30,
                TimeSpan.FromMinutes(60)),
        ];

        var viewModel = new VeloCenter.App.ViewModels.ProgressViewModel(activities);
        var currentYearSeries = Assert.Single(viewModel.AnnualSeries, series => series.Year == today.Year);

        Assert.Equal(today.DayOfYear, currentYearSeries.Points[^1].DayOfYear);
        Assert.Equal(today.DayOfYear < (DateTime.IsLeapYear(today.Year) ? 366 : 365), currentYearSeries.ShowEndMarker);
    }

    [Fact]
    public void ProgressViewModel_DoesNotShowEndMarkerForCompletedYear()
    {
        var pastYear = DateTime.Today.Year - 1;
        var localOffset = TimeZoneInfo.Local.GetUtcOffset(new DateTime(pastYear, 6, 1, 12, 0, 0));
        VeloCenter.Core.Activities.ActivitySummary[] activities =
        [
            new(
                Guid.NewGuid(),
                VeloCenter.Core.Activities.ActivitySource.Strava,
                "Past year ride",
                new DateTimeOffset(pastYear, 6, 1, 12, 0, 0, localOffset),
                60,
                TimeSpan.FromMinutes(120)),
        ];

        var viewModel = new VeloCenter.App.ViewModels.ProgressViewModel(activities);
        var completedYearSeries = Assert.Single(viewModel.AnnualSeries, series => series.Year == pastYear);

        Assert.False(completedYearSeries.ShowEndMarker);
    }

    [Fact]
    public void WorkoutsViewModel_PaginatesActivitiesByTen()
    {
        var offset = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 4, 1, 12, 0, 0));
        var activities = Enumerable.Range(1, 23)
            .Select(index => new VeloCenter.Core.Activities.ActivitySummary(
                Guid.NewGuid(),
                VeloCenter.Core.Activities.ActivitySource.Strava,
                $"Ride {index}",
                new DateTimeOffset(2026, 4, 24, 12, 0, 0, offset).AddDays(-index),
                40 + index,
                TimeSpan.FromMinutes(90 + index)))
            .ToArray();

        var viewModel = new VeloCenter.App.ViewModels.WorkoutsViewModel(activities, activities.Length, "Wszystkie");

        Assert.Equal(10, viewModel.RideLibrary.Count);
        Assert.True(viewModel.HasPagination);
        Assert.Equal("Strona 1 z 3  •  1-10 z 23", viewModel.PaginationLabel);

        ExecuteCommand(viewModel, "NextPageCommand");

        Assert.Equal(10, viewModel.RideLibrary.Count);
        Assert.Equal("Strona 2 z 3  •  11-20 z 23", viewModel.PaginationLabel);

        ExecuteCommand(viewModel, "NextPageCommand");

        Assert.Equal(3, viewModel.RideLibrary.Count);
        Assert.Equal("Strona 3 z 3  •  21-23 z 23", viewModel.PaginationLabel);
        Assert.False(viewModel.CanGoNextPage);
    }

    [Fact]
    public void LocalActivityRangePreferencesStore_SavesAndLoadsSelection()
    {
        var preferencesDirectory = Path.Combine(Path.GetTempPath(), "velo-center-tests", Guid.NewGuid().ToString("N"));
        var preferencesPath = Path.Combine(preferencesDirectory, "ui-preferences.json");

        try
        {
            var store = new VeloCenter.App.Services.LocalActivityRangePreferencesStore(preferencesPath);
            var selection = new VeloCenter.App.Models.ActivityRangeSelection(
                VeloCenter.App.Models.ActivityRangePreset.Custom,
                new DateTime(2026, 3, 1),
                new DateTime(2026, 3, 31));

            store.Save(selection);
            var loadedSelection = store.Load();

            Assert.Equal(VeloCenter.App.Models.ActivityRangePreset.Custom, loadedSelection.Preset);
            Assert.Equal(new DateTime(2026, 3, 1), loadedSelection.StartDate);
            Assert.Equal(new DateTime(2026, 3, 31), loadedSelection.EndDate);
        }
        finally
        {
            if (Directory.Exists(preferencesDirectory))
            {
                Directory.Delete(preferencesDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void MainWindowViewModel_RestoresSavedRangeSelectionForWorkouts()
    {
        var store = new VeloCenter.App.Services.InMemoryActivityRangePreferencesStore();
        var futureYear = DateTime.Today.Year + 1;
        var startDate = new DateTime(futureYear, 1, 1);
        var endDate = new DateTime(futureYear, 1, 31);

        store.Save(new VeloCenter.App.Models.ActivityRangeSelection(
            VeloCenter.App.Models.ActivityRangePreset.Custom,
            startDate,
            endDate));

        var viewModel = new VeloCenter.App.ViewModels.MainWindowViewModel(
            new VeloCenter.Infrastructure.Activities.InMemoryActivityRepository(),
            new VeloCenter.Infrastructure.Activities.InMemoryActivityImportService(),
            new VeloCenter.Infrastructure.Integrations.Strava.DisabledStravaIntegrationService(),
            new VeloCenter.Infrastructure.Maintenance.NoOpApplicationResetService(),
            store);
        var workoutsNavigationItem = Assert.Single(viewModel.NavigationItems, item => item.Title == "Treningi");

        ExecuteCommand(viewModel, "SelectSectionCommand", workoutsNavigationItem);

        var workoutsViewModel = Assert.IsType<VeloCenter.App.ViewModels.WorkoutsViewModel>(viewModel.CurrentSectionViewModel);

        Assert.Equal($"{startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}", viewModel.CurrentRangeLabel);
        Assert.True(workoutsViewModel.HasNoActivities);
        Assert.Equal("Brak treningow w wybranym zakresie", workoutsViewModel.EmptyLibraryTitle);
    }

    private static void ExecuteCommand(object target, string commandPropertyName, object? parameter = null)
    {
        var command = target.GetType().GetProperty(commandPropertyName)!.GetValue(target)!;
        var parameterType = parameter?.GetType();
        var executeMethod = command
            .GetType()
            .GetMethods()
            .Where(method => method.Name == "Execute" && method.GetParameters().Length == 1)
            .OrderBy(method =>
            {
                var methodParameterType = method.GetParameters()[0].ParameterType;

                if (parameterType is null)
                {
                    return methodParameterType == typeof(object) ? 0 : 1;
                }

                if (methodParameterType == parameterType)
                {
                    return 0;
                }

                return methodParameterType.IsAssignableFrom(parameterType) ? 1 : 2;
            })
            .First();

        executeMethod.Invoke(command, [parameter]);
    }
}
