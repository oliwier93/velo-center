namespace VeloCenter.App.ViewModels;

public sealed class ImportViewModel : ViewModelBase
{
    private string _importStatus = string.Empty;
    private string _stravaStatusTitle = "Strava nie jest jeszcze polaczona.";
    private string _stravaStatusDetail = "Wpisz dane swojej aplikacji Strava i polacz konto przez przegladarke.";
    private string _stravaConnectLabel = "Polacz Strave";
    private string _stravaSyncLabel = "Synchronizuj";
    private string _stravaClientId = string.Empty;
    private string _stravaClientSecret = string.Empty;
    private bool _canSyncStrava;
    private bool _isStravaBusy;

    public string AcceptedExtensionsLabel { get; } = "Akceptowane: .fit, .fit.gz, .gpx";

    public string ImportStatus
    {
        get => _importStatus;
        private set
        {
            if (SetProperty(ref _importStatus, value))
            {
                OnPropertyChanged(nameof(HasImportStatus));
            }
        }
    }

    public bool HasImportStatus => !string.IsNullOrWhiteSpace(ImportStatus);

    public string StravaClientId
    {
        get => _stravaClientId;
        set
        {
            if (SetProperty(ref _stravaClientId, value))
            {
                OnPropertyChanged(nameof(CanSaveStravaConfigurationAction));
            }
        }
    }

    public string StravaClientSecret
    {
        get => _stravaClientSecret;
        set
        {
            if (SetProperty(ref _stravaClientSecret, value))
            {
                OnPropertyChanged(nameof(CanSaveStravaConfigurationAction));
            }
        }
    }

    public string StravaStatusTitle
    {
        get => _stravaStatusTitle;
        private set => SetProperty(ref _stravaStatusTitle, value);
    }

    public string StravaStatusDetail
    {
        get => _stravaStatusDetail;
        private set => SetProperty(ref _stravaStatusDetail, value);
    }

    public string StravaConnectLabel
    {
        get => _stravaConnectLabel;
        private set => SetProperty(ref _stravaConnectLabel, value);
    }

    public string StravaSyncLabel
    {
        get => _stravaSyncLabel;
        private set => SetProperty(ref _stravaSyncLabel, value);
    }

    public bool CanSyncStrava
    {
        get => _canSyncStrava;
        private set
        {
            if (SetProperty(ref _canSyncStrava, value))
            {
                OnPropertyChanged(nameof(CanSyncStravaAction));
            }
        }
    }

    public bool IsStravaBusy
    {
        get => _isStravaBusy;
        private set
        {
            if (SetProperty(ref _isStravaBusy, value))
            {
                OnPropertyChanged(nameof(CanConnectStrava));
                OnPropertyChanged(nameof(CanSaveStravaConfigurationAction));
                OnPropertyChanged(nameof(CanSyncStravaAction));
            }
        }
    }

    public bool CanConnectStrava => !IsStravaBusy;

    public bool CanSyncStravaAction => CanSyncStrava && !IsStravaBusy;

    public bool CanSaveStravaConfigurationAction =>
        !IsStravaBusy &&
        !string.IsNullOrWhiteSpace(StravaClientId) &&
        !string.IsNullOrWhiteSpace(StravaClientSecret);

    public void SetImportQueued(string filePath)
    {
        ImportStatus = $"Importowanie: {System.IO.Path.GetFileName(filePath)}";
    }

    public void SetImportBlocked(string reason)
    {
        ImportStatus = reason;
    }

    public void SetImportCompleted(VeloCenter.Core.Activities.ActivityImportResult result)
    {
        var actionLabel = result.WasCreated ? "Zapisano" : "Zaktualizowano";
        ImportStatus = $"{actionLabel}: {result.Activity.Title}";
    }

    public void SetImportFailed(string reason)
    {
        ImportStatus = $"Blad importu: {reason}";
    }

    public void SetStravaState(VeloCenter.Core.Integrations.StravaConnectionState state)
    {
        if (!state.IsConfigured)
        {
            StravaStatusTitle = "Dodaj dane swojej aplikacji Strava";
            StravaStatusDetail = "Wklej Client ID i Client Secret, a potem polacz konto w przegladarce. Callback domain ustaw na 127.0.0.1.";
            StravaConnectLabel = "Polacz Strave";
            StravaSyncLabel = "Synchronizuj";
            CanSyncStrava = false;
            return;
        }

        if (!state.IsConnected)
        {
            StravaStatusTitle = "Strava gotowa do polaczenia";
            StravaStatusDetail = "Dane aplikacji zapisane. Kliknij polaczenie, zeby otworzyc przegladarke i zatwierdzic dostep.";
            StravaConnectLabel = "Polacz Strave";
            StravaSyncLabel = "Synchronizuj";
            CanSyncStrava = false;
            return;
        }

        StravaStatusTitle = string.IsNullOrWhiteSpace(state.AthleteName)
            ? "Polaczono ze Strava"
            : $"Polaczono: {state.AthleteName}";
        StravaStatusDetail = state.LastSyncedAt is null
            ? "Konto jest gotowe. Pierwsza synchronizacja pobierze tylko aktywnosci rowerowe outdoor."
            : $"Ostatnia synchronizacja roweru outdoor: {state.LastSyncedAt.Value.ToLocalTime():dd.MM.yyyy HH:mm}";
        StravaConnectLabel = "Polacz ponownie";
        StravaSyncLabel = "Synchronizuj";
        CanSyncStrava = true;
    }

    public void SetStravaBusyState(bool isBusy)
    {
        IsStravaBusy = isBusy;
        StravaConnectLabel = isBusy ? "Laczenie..." : StravaConnectLabel;
        StravaSyncLabel = isBusy ? "Synchronizacja..." : "Synchronizuj";
    }

    public void SetStravaSyncProgress(VeloCenter.Core.Integrations.StravaSyncProgress progress)
    {
        StravaStatusTitle = "Trwa synchronizacja Stravy";
        StravaStatusDetail = progress.Message;
    }

    public void SetStravaError(string message)
    {
        StravaStatusTitle = "Problem ze Strava";
        StravaStatusDetail = message;
        StravaConnectLabel = "Polacz Strave";
        StravaSyncLabel = "Synchronizuj";
    }
}
