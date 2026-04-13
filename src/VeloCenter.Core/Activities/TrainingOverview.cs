namespace VeloCenter.Core.Activities;

public sealed record TrainingOverview(
    int TotalActivities,
    double TotalDistanceKm,
    TimeSpan TotalDuration)
{
    public string TotalDistanceLabel => $"{TotalDistanceKm:0.0} km";

    public string TotalDurationLabel => TotalDuration.TotalHours >= 1
        ? $"{(int)TotalDuration.TotalHours}h {TotalDuration.Minutes}m"
        : $"{(int)TotalDuration.TotalMinutes} min";

    public static TrainingOverview FromActivities(IEnumerable<ActivitySummary> activities)
    {
        var items = activities.ToList();

        return new TrainingOverview(
            items.Count,
            items.Sum(activity => activity.DistanceKm),
            TimeSpan.FromMinutes(items.Sum(activity => activity.Duration.TotalMinutes)));
    }
}
