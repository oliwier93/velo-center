using VeloCenter.Core.Activities;

namespace VeloCenter.App.ViewModels;

public sealed class OverviewViewModel : ViewModelBase
{
    public OverviewViewModel(TrainingOverview overview, IReadOnlyList<ActivitySummary> activities)
    {
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
                .Select(activity => new RecentRideViewModel(activity)),
        ];

        NextSteps =
        [
            new InfoCardViewModel("Baza danych", "SQLite + EF Core", "Zastap repo in-memory trwalym magazynem lokalnym."),
            new InfoCardViewModel("Pierwszy importer", "FIT albo GPX", "Dowiez przeplyw od pliku do zapisanej aktywnosci."),
            new InfoCardViewModel("Analiza", "Trendy tygodniowe", "Dodaj wykresy objetosci, dystansu i czasu."),
        ];
    }

    public IReadOnlyList<MetricTileViewModel> Metrics { get; }

    public IReadOnlyList<RecentRideViewModel> RecentActivities { get; }

    public IReadOnlyList<InfoCardViewModel> NextSteps { get; }
}
