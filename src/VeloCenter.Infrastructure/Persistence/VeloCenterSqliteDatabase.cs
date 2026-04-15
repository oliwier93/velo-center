using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using VeloCenter.Core.Activities;

namespace VeloCenter.Infrastructure.Persistence;

public static class VeloCenterSqliteDatabase
{
    internal const string BaselineMigrationId = "20260414093000_InitialCreate";

    public static string GetApplicationDataDirectory()
    {
        var localApplicationDataRoot = Environment.GetEnvironmentVariable("LOCALAPPDATA");

        if (string.IsNullOrWhiteSpace(localApplicationDataRoot))
        {
            localApplicationDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        var preferredDirectory = EnsureDirectoryExists(Path.Combine(localApplicationDataRoot, "VeloCenter"));
        if (CanWriteToDirectory(preferredDirectory))
        {
            return preferredDirectory;
        }

        return EnsureDirectoryExists(Path.Combine(Path.GetTempPath(), "VeloCenter"));
    }

    public static string GetDefaultDatabasePath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("VELOCENTER_DB_PATH");

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return EnsureDatabaseDirectory(configuredPath);
        }

        var localApplicationDataRoot = Environment.GetEnvironmentVariable("LOCALAPPDATA");

        if (string.IsNullOrWhiteSpace(localApplicationDataRoot))
        {
            localApplicationDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        var preferredDirectory = EnsureDirectoryExists(Path.Combine(localApplicationDataRoot, "VeloCenter"));
        var preferredDatabasePath = Path.Combine(preferredDirectory, "velo-center.db");

        if (CanWriteToDirectory(preferredDirectory))
        {
            return preferredDatabasePath;
        }

        var fallbackDirectory = EnsureDirectoryExists(Path.Combine(Path.GetTempPath(), "VeloCenter"));
        var fallbackDatabasePath = Path.Combine(fallbackDirectory, "velo-center.db");
        TryCopyExistingDatabase(preferredDatabasePath, fallbackDatabasePath);
        return fallbackDatabasePath;
    }

    public static void Initialize(string databasePath, bool seedDemoData = false)
    {
        var dbContextOptions = CreateOptions(databasePath);

        using var dbContext = new VeloCenterDbContext(dbContextOptions);

        StampLegacyDatabaseIfNeeded(dbContext);
        dbContext.Database.Migrate();

        if (!seedDemoData || dbContext.Activities.Any())
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

    private static void StampLegacyDatabaseIfNeeded(VeloCenterDbContext dbContext)
    {
        var connection = dbContext.Database.GetDbConnection();

        connection.Open();

        try
        {
            if (TableExists(connection, "__EFMigrationsHistory") || !TableExists(connection, "activities"))
            {
                return;
            }

            using var createHistoryCommand = connection.CreateCommand();
            createHistoryCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );
                """;
            createHistoryCommand.ExecuteNonQuery();

            using var insertHistoryCommand = connection.CreateCommand();
            insertHistoryCommand.CommandText =
                """
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ($migrationId, $productVersion);
                """;

            AddParameter(insertHistoryCommand, "$migrationId", BaselineMigrationId);
            AddParameter(insertHistoryCommand, "$productVersion", "10.0.0");
            insertHistoryCommand.ExecuteNonQuery();
        }
        finally
        {
            connection.Close();
        }
    }

    private static bool TableExists(DbConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = $tableName;
            """;

        AddParameter(command, "$tableName", tableName);

        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string EnsureDatabaseDirectory(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var databaseDirectory = Path.GetDirectoryName(databasePath);

        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            EnsureDirectoryExists(databaseDirectory);
        }

        return databasePath;
    }

    private static string EnsureDirectoryExists(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private static bool CanWriteToDirectory(string directoryPath)
    {
        try
        {
            var probePath = Path.Combine(directoryPath, $".write-probe-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void TryCopyExistingDatabase(string sourceDatabasePath, string destinationDatabasePath)
    {
        if (File.Exists(destinationDatabasePath) || !File.Exists(sourceDatabasePath))
        {
            return;
        }

        TryCopyFile(sourceDatabasePath, destinationDatabasePath);
        TryCopyFile($"{sourceDatabasePath}-wal", $"{destinationDatabasePath}-wal");
        TryCopyFile($"{sourceDatabasePath}-shm", $"{destinationDatabasePath}-shm");
    }

    private static void TryCopyFile(string sourcePath, string destinationPath)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                return;
            }

            File.Copy(sourcePath, destinationPath, overwrite: false);
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }
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
                ImportFingerprint = "fit-seed-2ec213f8-6c5d-4af1-8821-7df59c2ad8f1",
                Title = "Sweet spot ride",
                StartTime = today.AddDays(-1).AddHours(6),
                DistanceKm = 48.6,
                DurationSeconds = (int)TimeSpan.FromMinutes(101).TotalSeconds,
                ImportedAt = today,
                LastUpdatedAt = today,
            },
            new ActivityRecord
            {
                Id = Guid.Parse("0ea6ec0d-46f8-4ce4-97da-769c9ba93c7d"),
                Source = ActivitySource.Strava,
                SourceActivityId = "strava-seed-0ea6ec0d-46f8-4ce4-97da-769c9ba93c7d",
                Title = "Endurance spin",
                StartTime = today.AddDays(-3).AddHours(7),
                DistanceKm = 62.2,
                DurationSeconds = (int)TimeSpan.FromMinutes(134).TotalSeconds,
                ImportedAt = today,
                LastUpdatedAt = today,
            },
            new ActivityRecord
            {
                Id = Guid.Parse("d6c4e8a6-56ab-4f7f-8a8f-7243d33df11d"),
                Source = ActivitySource.GpxFile,
                ImportFingerprint = "gpx-seed-d6c4e8a6-56ab-4f7f-8a8f-7243d33df11d",
                Title = "Recovery ride",
                StartTime = today.AddDays(-5).AddHours(18),
                DistanceKm = 24.8,
                DurationSeconds = (int)TimeSpan.FromMinutes(55).TotalSeconds,
                ImportedAt = today,
                LastUpdatedAt = today,
            },
        ];
    }
}
