using VeloCenter.Core.Activities;

namespace VeloCenter.App.ViewModels;

public sealed class OverviewViewModel : ViewModelBase
{
    public OverviewViewModel(TrainingOverview overview, IReadOnlyList<ActivitySummary> activities)
    {
        HasActivities = activities.Count > 0;

        Metrics =
        [
            new MetricTileViewModel("Aktywnosci", overview.TotalActivities.ToString(), "Wczytane do startowego widoku."),
            new MetricTileViewModel("Dystans", overview.TotalDistanceLabel, "Lacznie w aktualnej probce."),
            new MetricTileViewModel("Czas", overview.TotalDurationLabel, "Lacznie dla zaimportowanych treningow."),
        ];

        RecentActivities =
        [
            .. activities
                .OrderByDescending(activity => activity.StartTime)
                .Take(3)
                .Select(activity => new RecentRideViewModel(activity)),
        ];
    }

    public bool HasActivities { get; }

    public bool HasNoActivities => !HasActivities;

    public IReadOnlyList<MetricTileViewModel> Metrics { get; }

    public IReadOnlyList<RecentRideViewModel> RecentActivities { get; }
}
