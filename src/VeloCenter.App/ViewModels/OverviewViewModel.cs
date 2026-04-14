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
                .Select(activity => new RecentRideViewModel(activity)),
        ];

        NextSteps =
        [
            new InfoCardViewModel("Baza danych", "SQLite + EF Core", HasActivities
                ? "Trwaly magazyn jest gotowy na prawdziwe importy."
                : "Baza startuje pusta, wiec od razu widac realny stan aplikacji."),
            new InfoCardViewModel("Pierwszy importer", "FIT albo GPX", HasActivities
                ? "Lokalny import zapisuje juz dane do SQLite i odswieza widoki."
                : "Wybierz plik w sekcji integracje, aby zapisac pierwsza aktywnosc."),
            new InfoCardViewModel("Analiza", "Trendy tygodniowe", "Dodaj wykresy objetosci, dystansu i czasu."),
        ];
    }

    public bool HasActivities { get; }

    public bool HasNoActivities => !HasActivities;

    public IReadOnlyList<MetricTileViewModel> Metrics { get; }

    public IReadOnlyList<RecentRideViewModel> RecentActivities { get; }

    public IReadOnlyList<InfoCardViewModel> NextSteps { get; }
}
