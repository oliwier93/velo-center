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
}
