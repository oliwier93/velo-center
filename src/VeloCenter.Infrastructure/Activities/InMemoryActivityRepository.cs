using VeloCenter.Core.Activities;

namespace VeloCenter.Infrastructure.Activities;

public sealed class InMemoryActivityRepository : IActivityRepository
{
    public IReadOnlyList<ActivitySummary> GetRecentActivities()
    {
        var today = DateTimeOffset.Now.Date;

        return
        [
            new ActivitySummary(
                Guid.Parse("2ec213f8-6c5d-4af1-8821-7df59c2ad8f1"),
                ActivitySource.FitFile,
                "Sweet spot ride",
                today.AddDays(-1).AddHours(6),
                48.6,
                TimeSpan.FromMinutes(101)),
            new ActivitySummary(
                Guid.Parse("0ea6ec0d-46f8-4ce4-97da-769c9ba93c7d"),
                ActivitySource.Strava,
                "Endurance spin",
                today.AddDays(-3).AddHours(7),
                62.2,
                TimeSpan.FromMinutes(134)),
            new ActivitySummary(
                Guid.Parse("d6c4e8a6-56ab-4f7f-8a8f-7243d33df11d"),
                ActivitySource.GpxFile,
                "Recovery ride",
                today.AddDays(-5).AddHours(18),
                24.8,
                TimeSpan.FromMinutes(55)),
        ];
    }
}
