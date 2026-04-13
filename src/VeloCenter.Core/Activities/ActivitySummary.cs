namespace VeloCenter.Core.Activities;

public sealed record ActivitySummary(
    Guid Id,
    ActivitySource Source,
    string Title,
    DateTimeOffset StartTime,
    double DistanceKm,
    TimeSpan Duration)
{
    public string SourceLabel => Source switch
    {
        ActivitySource.GpxFile => "GPX import",
        ActivitySource.FitFile => "FIT import",
        ActivitySource.Strava => "Strava",
        _ => "Manual",
    };

    public string StartDateLabel => StartTime.ToLocalTime().ToString("dd MMM yyyy");

    public string DistanceLabel => $"{DistanceKm:0.0} km";

    public string DurationLabel => Duration.TotalHours >= 1
        ? $"{(int)Duration.TotalHours}h {Duration.Minutes}m"
        : $"{(int)Duration.TotalMinutes} min";
}
