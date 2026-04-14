using Microsoft.EntityFrameworkCore.Design;

namespace VeloCenter.Infrastructure.Persistence;

public sealed class VeloCenterDbContextFactory : IDesignTimeDbContextFactory<VeloCenterDbContext>
{
    public VeloCenterDbContext CreateDbContext(string[] args)
    {
        var databasePath = VeloCenterSqliteDatabase.GetDefaultDatabasePath();
        var options = VeloCenterSqliteDatabase.CreateOptions(databasePath);

        return new VeloCenterDbContext(options);
    }
}
