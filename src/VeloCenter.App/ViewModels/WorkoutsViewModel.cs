using VeloCenter.Core.Activities;

namespace VeloCenter.App.ViewModels;

public sealed class WorkoutsViewModel : ViewModelBase
{
    public WorkoutsViewModel(IReadOnlyList<ActivitySummary> activities)
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

    public IReadOnlyList<MetricTileViewModel> Highlights { get; }

    public IReadOnlyList<RecentRideViewModel> RideLibrary { get; }

    public IReadOnlyList<InfoCardViewModel> FilterIdeas { get; }
}
