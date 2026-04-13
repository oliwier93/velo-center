namespace VeloCenter.App.ViewModels;

public sealed class ImportViewModel : ViewModelBase
{
    public ImportViewModel()
    {
        Sources =
        [
            new InfoCardViewModel("FIT files", "Ready next", "Najlepszy kierunek na pierwszy realny importer sportowy."),
            new InfoCardViewModel("GPX files", "Ready next", "Szybki bootstrap dla prostych przejazdow i testow."),
            new InfoCardViewModel("Strava", "Planned", "OAuth i sync przyrostowy dopniemy po lokalnym imporcie plikow."),
        ];

        Pipeline =
        [
            new InfoCardViewModel("Krok 1", "Wczytaj plik", "Odczytaj metadane, czas startu i podstawowe statystyki."),
            new InfoCardViewModel("Krok 2", "Zapisz lokalnie", "Przenies aktywnosc do SQLite razem z import jobem."),
            new InfoCardViewModel("Krok 3", "Przelicz analityke", "Uaktualnij dashboard, listy i trendy."),
        ];
    }

    public IReadOnlyList<InfoCardViewModel> Sources { get; }

    public IReadOnlyList<InfoCardViewModel> Pipeline { get; }
}
