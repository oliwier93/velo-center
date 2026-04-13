namespace VeloCenter.App.ViewModels;

public sealed class RecentRideViewModel
{
    public RecentRideViewModel(VeloCenter.Core.Activities.ActivitySummary activity)
    {
        Title = activity.Title;
        StartDateLabel = activity.StartDateLabel;
        DistanceLabel = activity.DistanceLabel;
        DurationLabel = activity.DurationLabel;
        SourceLabel = activity.SourceLabel;
    }

    public string Title { get; }

    public string StartDateLabel { get; }

    public string DistanceLabel { get; }

    public string DurationLabel { get; }

    public string SourceLabel { get; }
}
