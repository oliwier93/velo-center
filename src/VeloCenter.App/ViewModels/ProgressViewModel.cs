using VeloCenter.Core.Activities;

namespace VeloCenter.App.ViewModels;

public sealed class ProgressViewModel : ViewModelBase
{
    public ProgressViewModel(IReadOnlyList<ActivitySummary> activities)
    {
        var longestRide = activities.OrderByDescending(activity => activity.DistanceKm).First();
        var activeDays = activities
            .Select(activity => activity.StartTime.Date)
            .Distinct()
            .Count();
        var averageMinutes = activities.Average(activity => activity.Duration.TotalMinutes);

        Highlights =
        [
            new MetricTileViewModel("Aktywne dni", activeDays.ToString(), "Dni z przynajmniej jednym treningiem."),
            new MetricTileViewModel("Najdluzszy przejazd", longestRide.DistanceLabel, longestRide.Title),
            new MetricTileViewModel("Sredni czas", $"{averageMinutes:0} min", "Na aktywnosc w tym zestawie."),
        ];

        Signals =
        [
            new InfoCardViewModel("Spojnosc", "Buduje sie", "Masz juz shell pod tygodniowe trendy i porownania blokow treningowych."),
            new InfoCardViewModel("Moc i tetno", "Jeszcze offline", "Po importerze FIT bedzie sens liczyc strefy, IF i TSS."),
            new InfoCardViewModel("Nastepny unlock", "CTL / ATL / TSB", "Te metryki warto dorzucic po stabilnym modelu danych."),
        ];

        FocusAreas =
        [
            new InfoCardViewModel("Trend tygodniowy", "Volume first", "Najpierw zrob dystans, czas i liczbe aktywnosci per tydzien."),
            new InfoCardViewModel("PR i best efforts", "Etap 2", "Dobrze wejda po imporcie streamow albo danych mocy."),
        ];
    }

    public IReadOnlyList<MetricTileViewModel> Highlights { get; }

    public IReadOnlyList<InfoCardViewModel> Signals { get; }

    public IReadOnlyList<InfoCardViewModel> FocusAreas { get; }
}
