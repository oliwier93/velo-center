using VeloCenter.Core.Activities;

namespace VeloCenter.App.ViewModels;

public sealed class HeatmapViewModel : ViewModelBase
{
    public HeatmapViewModel(
        IReadOnlyList<ActivityRoute> routes,
        string rangeLabel,
        int totalActivitiesCount)
    {
        Routes =
        [
            .. routes
                .Where(route => route.Points.Count > 1)
                .OrderBy(route => route.StartTime)
                .Select(route => new HeatmapRouteViewModel(
                    route.ActivityId,
                    route.Title,
                    route.Source switch
                    {
                        ActivitySource.GpxFile => "GPX import",
                        ActivitySource.FitFile => "FIT import",
                        ActivitySource.Strava => "Strava",
                        _ => "Manual",
                    },
                    route.StartTime,
                    [
                        .. route.Points.Select(point => new HeatmapPointViewModel(point.Latitude, point.Longitude)),
                    ])),
        ];

        HasRoutes = Routes.Count > 0;
        HasNoRoutes = !HasRoutes;

        EmptyTitle = totalActivitiesCount == 0
            ? "Brak aktywnosci do narysowania"
            : "Brak tras w wybranym zakresie";
        EmptyDescription = totalActivitiesCount == 0
            ? "Heatmapa zacznie zyć po imporcie GPX albo po synchronizacji Stravy z zapisanymi sladowymi trasami."
            : $"W zakresie {rangeLabel.ToLowerInvariant()} nie ma jeszcze aktywnosci z zapisana geometria trasy.";

        var pointCount = Routes.Sum(route => route.Points.Count);

        EmptyDescription = totalActivitiesCount == 0
            ? "Heatmapa ruszy po imporcie GPX albo po synchronizacji Stravy z zapisana geometria tras."
            : $"W zakresie {rangeLabel.ToLowerInvariant()} nie ma jeszcze aktywnosci z zapisana geometria trasy.";

        Highlights =
        [
            new MetricTileViewModel("Trasy na mapie", Routes.Count.ToString(), $"Zakres: {rangeLabel.ToLowerInvariant()}."),
            new MetricTileViewModel("Punkty tras", pointCount.ToString("N0"), "Kazdy punkt doklada natezenie do heatmapy."),
        ];
    }

    public IReadOnlyList<HeatmapRouteViewModel> Routes { get; }

    public IReadOnlyList<MetricTileViewModel> Highlights { get; }

    public bool HasRoutes { get; }

    public bool HasNoRoutes { get; }

    public string EmptyTitle { get; }

    public string EmptyDescription { get; }
}
