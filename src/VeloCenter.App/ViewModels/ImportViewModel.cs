namespace VeloCenter.App.ViewModels;

public sealed class ImportViewModel : ViewModelBase
{
    private string _selectedFileName = "Brak wybranego pliku";
    private string _selectedFileDescription = "Wybierz plik FIT albo GPX, zeby sprawdzic pusty stan aplikacji i loader na dolnym pasku.";
    private string _lastImportState = "Oczekiwanie";
    private string _lastImportDetail = "Loader uruchomi sie dopiero po wskazaniu lokalnego pliku.";

    public ImportViewModel()
    {
        Sources =
        [
            new InfoCardViewModel("FIT files", "Ready now", "Pierwszy kandydat do testowania lokalnego loadera i przyszlego parsera."),
            new InfoCardViewModel("GPX files", "Ready now", "Dobry format na szybkie sprawdzenie pustego stanu i przeplywu UI."),
            new InfoCardViewModel("Strava", "Planned", "OAuth i sync przyrostowy dopniemy po lokalnym imporcie plikow."),
        ];

        Pipeline =
        [
            new InfoCardViewModel("Krok 1", "Wczytaj plik", "Odczytaj metadane, czas startu i podstawowe statystyki."),
            new InfoCardViewModel("Krok 2", "Zapisz lokalnie", "Przenies aktywnosc do SQLite razem z import jobem."),
            new InfoCardViewModel("Krok 3", "Przelicz analityke", "Uaktualnij dashboard, listy i trendy."),
        ];
    }

    public string SelectedFileName
    {
        get => _selectedFileName;
        private set => SetProperty(ref _selectedFileName, value);
    }

    public string SelectedFileDescription
    {
        get => _selectedFileDescription;
        private set => SetProperty(ref _selectedFileDescription, value);
    }

    public string LastImportState
    {
        get => _lastImportState;
        private set => SetProperty(ref _lastImportState, value);
    }

    public string LastImportDetail
    {
        get => _lastImportDetail;
        private set => SetProperty(ref _lastImportDetail, value);
    }

    public IReadOnlyList<InfoCardViewModel> Sources { get; }

    public IReadOnlyList<InfoCardViewModel> Pipeline { get; }

    public void SetSelectedFile(string filePath)
    {
        var fileName = System.IO.Path.GetFileName(filePath);

        SelectedFileName = fileName;
        SelectedFileDescription = filePath;
        LastImportState = "W kolejce";
        LastImportDetail = "Plik zostal przekazany do testowego loadera i przechodzi przez frontendowy przeplyw.";
    }

    public void SetImportBlocked(string reason)
    {
        LastImportState = "Zajete";
        LastImportDetail = reason;
    }

    public void SetImportCompleted(string filePath)
    {
        var fileName = System.IO.Path.GetFileName(filePath);

        SelectedFileName = fileName;
        SelectedFileDescription = filePath;
        LastImportState = "Gotowe";
        LastImportDetail = "Plik przeszedl przez loader UI. Zapis do bazy podlaczemy w kolejnym kroku.";
    }
}
