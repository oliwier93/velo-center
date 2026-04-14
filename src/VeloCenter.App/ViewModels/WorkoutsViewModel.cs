using VeloCenter.Core.Activities;

namespace VeloCenter.App.ViewModels;

public sealed class WorkoutsViewModel : ViewModelBase
{
    public WorkoutsViewModel(IReadOnlyList<ActivitySummary> activities)
    {
        HasActivities = activities.Count > 0;

        if (HasActivities)
        {
            var latestRide = activities.OrderByDescending(activity => activity.StartTime).First();
            var longestRide = activities.OrderByDescending(activity => activity.DistanceKm).First();
            var averageDistance = activities.Average(activity => activity.DistanceKm);

            Highlights =
            [
                new MetricTileViewModel("Wczytane treningi", activities.Count.ToString(), "Startowa biblioteka aktywnosci."),
                new MetricTileViewModel("Najnowszy przejazd", latestRide.DistanceLabel, latestRide.Title),
                new MetricTileViewModel("Sredni dystans", $"{averageDistance:0.0} km", "Na aktywnosc w aktualnym zestawie."),
                new MetricTileViewModel("Najdluzszy trening", longestRide.DistanceLabel, longestRide.Title),
            ];
        }
        else
        {
            Highlights =
            [
                new MetricTileViewModel("Wczytane treningi", "0", "Biblioteka czeka na pierwszy import."),
                new MetricTileViewModel("Najnowszy przejazd", "--", "Pojawi sie po pierwszym pliku FIT albo GPX."),
                new MetricTileViewModel("Sredni dystans", "--", "Potrzebujemy przynajmniej jednej aktywnosci."),
                new MetricTileViewModel("Najdluzszy trening", "--", "Na razie baza jest celowo pusta."),
            ];
        }

        RideLibrary =
        [
            .. activities
                .OrderByDescending(activity => activity.StartTime)
                .Select(activity => new RecentRideViewModel(activity)),
        ];

        FilterIdeas =
        [
            new InfoCardViewModel("Zakres dat", "Ostatnie 30 dni", "Najprostszy filtr do regularnej pracy z danymi."),
            new InfoCardViewModel("Zrodlo danych", "FIT / GPX / Strava", "Pozwoli szybciej diagnozowac import i sync."),
            new InfoCardViewModel("Intensywnosc", "Easy / Tempo / Hard", "Warto dodac po podpieciu mocy albo tetna."),
        ];
    }

    public bool HasActivities { get; }

    public bool HasNoActivities => !HasActivities;

    public IReadOnlyList<MetricTileViewModel> Highlights { get; }

    public IReadOnlyList<RecentRideViewModel> RideLibrary { get; }

    public IReadOnlyList<InfoCardViewModel> FilterIdeas { get; }
}
