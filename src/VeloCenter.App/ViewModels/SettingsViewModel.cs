namespace VeloCenter.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel()
    {
        Workspace =
        [
            new InfoCardViewModel("Profil ridera", "Single athlete", "Jeden lokalny profil wystarczy na pierwszy etap produktu."),
            new InfoCardViewModel("Storage", "SQLite next", "Docelowa baza powinna siedziec lokalnie obok aplikacji."),
            new InfoCardViewModel("Units", "Metric", "Dystans, predkosc i przewyzszenia w kilometrach i metrach."),
        ];

        Preferences =
        [
            new InfoCardViewModel("Theme", "Liquid dark", "Projekt jedzie na ciemnym shellu z fioletowo-rozowym akcentem."),
            new InfoCardViewModel("Import defaults", "Manual", "Pozniej dodamy ostatni katalog i domyslne mapowanie zrodel."),
            new InfoCardViewModel("Sync policy", "On demand", "Automatyczny sync warto odpalic po stabilizacji API."),
        ];
    }

    public IReadOnlyList<InfoCardViewModel> Workspace { get; }

    public IReadOnlyList<InfoCardViewModel> Preferences { get; }
}
