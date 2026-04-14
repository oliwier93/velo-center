using Microsoft.Data.Sqlite;
using VeloCenter.Core.Maintenance;
using VeloCenter.Infrastructure.Persistence;

namespace VeloCenter.Infrastructure.Maintenance;

public sealed class LocalApplicationResetService(string databasePath) : IApplicationResetService
{
    public void ResetAllData()
    {
        SqliteConnection.ClearAllPools();

        var applicationDataDirectory = VeloCenterSqliteDatabase.GetApplicationDataDirectory();

        DeleteFileIfExists(databasePath);
        DeleteFileIfExists($"{databasePath}-wal");
        DeleteFileIfExists($"{databasePath}-shm");
        DeleteFileIfExists($"{databasePath}-journal");

        if (Directory.Exists(applicationDataDirectory))
        {
            Directory.Delete(applicationDataDirectory, recursive: true);
        }

        VeloCenterSqliteDatabase.Initialize(databasePath, seedDemoData: false);
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
