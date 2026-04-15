namespace VeloCenter.App.ViewModels;

using VeloCenter.App.Models;
using VeloCenter.App.Services;


public sealed class MainWindowViewModel : ViewModelBase
{
    private enum PipelineTaskKind
    {
        None,
        Import,
        Sync,
        Maintenance,
    }

    private readonly VeloCenter.Core.Activities.IActivityRepository _activityRepository;
    private readonly VeloCenter.Core.Activities.IActivityImportService _activityImportService;
    private readonly VeloCenter.Core.Integrations.IStravaIntegrationService _stravaIntegrationService;
    private readonly VeloCenter.Core.Maintenance.IApplicationResetService _applicationResetService;
    private readonly IActivityRangePreferencesStore _activityRangePreferencesStore;
    private OverviewViewModel _overviewViewModel = null!;
    private WorkoutsViewModel _workoutsViewModel = null!;
    private ProgressViewModel _progressViewModel = null!;
    private readonly ImportViewModel _importViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly NavigationItemViewModel _overviewNavigationItem;
    private readonly NavigationItemViewModel _importNavigationItem;
    private readonly ActivityRangeOptionViewModel _last30DaysRangeOption;
    private readonly ActivityRangeOptionViewModel _thisMonthRangeOption;
    private readonly ActivityRangeOptionViewModel _thisYearRangeOption;
    private readonly ActivityRangeOptionViewModel _customRangeOption;
    private readonly ActivityRangeOptionViewModel _allRangeOption;
    private readonly Avalonia.Threading.DispatcherTimer _taskStripeTimer;
    private readonly System.Diagnostics.Stopwatch _taskStripeStopwatch;
    private IReadOnlyList<VeloCenter.Core.Activities.ActivitySummary> _allActivities = [];
    private ActivityRangeSelection _currentRangeSelection = ActivityRangeSelection.Default;
    private ActivityRangeOptionViewModel _selectedRangeOption = null!;
    private bool _hasActivities;

    private bool _isSidebarExpanded;
    private bool _isSidebarContentExpanded;
    private string _currentSectionKey = string.Empty;
    private string _currentSectionTitle = string.Empty;
    private string _currentSectionDescription = string.Empty;
    private ViewModelBase _currentSectionViewModel = null!;
    private string _statusState = string.Empty;
    private string _statusMessage = string.Empty;
    private string _activeTask = string.Empty;
    private string _lastActionLabel = string.Empty;
    private string _statusBadgeBackground = "#2A1734";
    private string _statusBadgeForeground = "#F7E9FF";
    private PipelineTaskKind _currentPipelineTask;
    private string _taskTitle = string.Empty;
    private string _taskDetail = string.Empty;
    private double _taskProgressValue;
    private double _taskStripeOffset;
    private bool _isToastVisible;
    private string _toastMessage = string.Empty;
    private string _toastBackground = "#20342C";
    private string _toastForeground = "#F3FFF8";
    private int _toastVersion;

    public MainWindowViewModel()
        : this(
            new VeloCenter.Infrastructure.Activities.InMemoryActivityRepository(),
            new VeloCenter.Infrastructure.Activities.InMemoryActivityImportService(),
            new VeloCenter.Infrastructure.Integrations.Strava.DisabledStravaIntegrationService(),
            new VeloCenter.Infrastructure.Maintenance.NoOpApplicationResetService(),
            new InMemoryActivityRangePreferencesStore())
    {
    }

    public MainWindowViewModel(
        VeloCenter.Core.Activities.IActivityRepository activityRepository,
        VeloCenter.Core.Activities.IActivityImportService activityImportService,
        VeloCenter.Core.Integrations.IStravaIntegrationService stravaIntegrationService,
        VeloCenter.Core.Maintenance.IApplicationResetService applicationResetService,
        IActivityRangePreferencesStore activityRangePreferencesStore)
    {
        _activityRepository = activityRepository;
        _activityImportService = activityImportService;
        _stravaIntegrationService = stravaIntegrationService;
        _applicationResetService = applicationResetService;
        _activityRangePreferencesStore = activityRangePreferencesStore;
        _importViewModel = new ImportViewModel();
        _settingsViewModel = new SettingsViewModel();
        _last30DaysRangeOption = new ActivityRangeOptionViewModel(ActivityRangePreset.Last30Days, "Ostatnie 30 dni");
        _thisMonthRangeOption = new ActivityRangeOptionViewModel(ActivityRangePreset.ThisMonth, "Ten miesiac");
        _thisYearRangeOption = new ActivityRangeOptionViewModel(ActivityRangePreset.ThisYear, "Ten rok");
        _customRangeOption = new ActivityRangeOptionViewModel(ActivityRangePreset.Custom, "Zakres od-do");
        _allRangeOption = new ActivityRangeOptionViewModel(ActivityRangePreset.All, "Wszystkie");

        _overviewNavigationItem = new NavigationItemViewModel(
            "overview",
            "Przeglad",
            "Szybki obraz tygodnia, obciazenia i ostatnich przejazdow.",
            "M5,5 H11 V11 H5 Z M13,5 H19 V9 H13 Z M5,13 H10 V19 H5 Z M12,12 H19 V19 H12 Z");
        var workoutsNavigationItem = new NavigationItemViewModel(
            "workouts",
            "Treningi",
            "Biblioteka przejazdow i podglad danych z aktywnosci.",
            "M4,16 C7,11 10,11 13,14 C15,16 17,16 20,8 M5.5,16 A1.5,1.5 0 1 0 5.6,16 M13,14 A1.5,1.5 0 1 0 13.1,14 M20,8 A1.5,1.5 0 1 0 20.1,8");
        var progressNavigationItem = new NavigationItemViewModel(
            "progress",
            "Podsumowanie",
            "Roczne porownanie kilometrow i przebiegu sezonu rok do roku.",
            "M5,17 L10,12 L14,14 L19,7 M15,7 H19 V11");
        _importNavigationItem = new NavigationItemViewModel(
            "import",
            "Integracje",
            "Strava, pliki lokalne i kolejne zrodla danych.",
            "M7,7 H11 V11 H7 Z M13,13 H17 V17 H13 Z M9,9 L15,15 M15,9 L9,15 M15,5 H19 V9 H15 Z M5,15 H9 V19 H5 Z");
        var settingsNavigationItem = new NavigationItemViewModel(
            "settings",
            "Ustawienia",
            "Reset danych lokalnych, integracji i stanu aplikacji.",
            "M6,7 H18 M6,12 H18 M6,17 H18 M9,7 A2,2 0 1 0 9.1,7 M15,12 A2,2 0 1 0 15.1,12 M11,17 A2,2 0 1 0 11.1,17");

        NavigationItems =
        [
            _overviewNavigationItem,
            workoutsNavigationItem,
            progressNavigationItem,
            _importNavigationItem,
            settingsNavigationItem,
        ];

        RangeOptions =
        [
            _last30DaysRangeOption,
            _thisMonthRangeOption,
            _thisYearRangeOption,
            _customRangeOption,
            _allRangeOption,
        ];
        ApplyRangeSelection(_activityRangePreferencesStore.Load(), persist: false);

        SelectSectionCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<NavigationItemViewModel?>(SelectSection);
        SyncCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => _ = RunToolbarSyncAsync());

        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.ActivateCommand = SelectSectionCommand;
        }

        ReloadActivitySections();
        RefreshStravaState();
        ApplySidebarStateToNavigationItems();
        SelectSection(_hasActivities ? _overviewNavigationItem : _importNavigationItem);
        SetTaskMonitorIdle("Loader uruchomi sie przy imporcie pliku albo synchronizacji Strava.");

        _taskStripeStopwatch = System.Diagnostics.Stopwatch.StartNew();
        _taskStripeTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(16),
        };
        _taskStripeTimer.Tick += (_, _) =>
        {
            const double stripePeriod = 32;
            const double stripeSpeed = 80;

            TaskStripeOffset = (_taskStripeStopwatch.Elapsed.TotalSeconds * stripeSpeed) % stripePeriod;
        };
        _taskStripeTimer.Start();

        UpdateStatus(
            _hasActivities ? "Ready" : "Empty",
            _hasActivities
                ? "Aplikacja zaladowala aktywnosci z lokalnej bazy."
                : "Baza jest pusta. Zacznij od sekcji integracji i zaimportuj pierwszy plik.",
            _hasActivities
                ? "Brak zadania w tle."
                : "Loader pozostaje ukryty, dopoki nie uruchomisz importu albo synchronizacji.",
            "#2A1734",
            "#F7E9FF");
    }

    public string AppTitle { get; } = "Velo Center";

    public string AppVersionLabel { get; } = $"v{ResolveApplicationVersion()}";

    public string AppAuthorLabel { get; } = "Oliwier Baran";

    public string AppMetaLabel => $"{AppVersionLabel}  •  {AppAuthorLabel}";

    public string CurrentRangeLabel => SelectedRangeOption.Label;

    public bool ShowTopBarRangeSelector => string.Equals(_currentSectionKey, "workouts", StringComparison.Ordinal);

    public bool UseViewportSectionLayout => string.Equals(_currentSectionKey, "workouts", StringComparison.Ordinal);

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        private set
        {
            if (SetProperty(ref _isSidebarExpanded, value))
            {
                OnPropertyChanged(nameof(SidebarWidth));
            }
        }
    }

    public bool IsSidebarContentExpanded
    {
        get => _isSidebarContentExpanded;
        private set
        {
            if (SetProperty(ref _isSidebarContentExpanded, value))
            {
                OnPropertyChanged(nameof(IsSidebarContentCollapsed));
                ApplySidebarStateToNavigationItems();
            }
        }
    }

    public bool IsSidebarContentCollapsed => !IsSidebarContentExpanded;

    public double SidebarWidth => IsSidebarExpanded ? 264 : 80;

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public IReadOnlyList<ActivityRangeOptionViewModel> RangeOptions { get; }

    public ActivityRangeOptionViewModel SelectedRangeOption
    {
        get => _selectedRangeOption;
        set
        {
            if (SetProperty(ref _selectedRangeOption, value))
            {
                OnPropertyChanged(nameof(CurrentRangeLabel));
            }
        }
    }

    public string CurrentSectionTitle
    {
        get => _currentSectionTitle;
        private set => SetProperty(ref _currentSectionTitle, value);
    }

    public string CurrentSectionDescription
    {
        get => _currentSectionDescription;
        private set => SetProperty(ref _currentSectionDescription, value);
    }

    public ViewModelBase CurrentSectionViewModel
    {
        get => _currentSectionViewModel;
        private set => SetProperty(ref _currentSectionViewModel, value);
    }

    public string StatusState
    {
        get => _statusState;
        private set => SetProperty(ref _statusState, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ActiveTask
    {
        get => _activeTask;
        private set => SetProperty(ref _activeTask, value);
    }

    public string LastActionLabel
    {
        get => _lastActionLabel;
        private set => SetProperty(ref _lastActionLabel, value);
    }

    public string StatusBadgeBackground
    {
        get => _statusBadgeBackground;
        private set => SetProperty(ref _statusBadgeBackground, value);
    }

    public string StatusBadgeForeground
    {
        get => _statusBadgeForeground;
        private set => SetProperty(ref _statusBadgeForeground, value);
    }

    public string TaskTitle
    {
        get => _taskTitle;
        private set => SetProperty(ref _taskTitle, value);
    }

    public string TaskDetail
    {
        get => _taskDetail;
        private set => SetProperty(ref _taskDetail, value);
    }

    public double TaskProgressValue
    {
        get => _taskProgressValue;
        private set
        {
            if (SetProperty(ref _taskProgressValue, value))
            {
                OnPropertyChanged(nameof(TaskProgressLabel));
                OnPropertyChanged(nameof(TaskProgressFillWidth));
            }
        }
    }

    public string TaskProgressLabel => $"{TaskProgressValue:0}%";

    public double TaskProgressFillWidth => 4.2 * TaskProgressValue;

    public double TaskStripeOffset
    {
        get => _taskStripeOffset;
        private set => SetProperty(ref _taskStripeOffset, value);
    }

    public bool IsTaskMonitorVisible => _currentPipelineTask is not PipelineTaskKind.None;

    public bool IsToastVisible
    {
        get => _isToastVisible;
        private set => SetProperty(ref _isToastVisible, value);
    }

    public string ToastMessage
    {
        get => _toastMessage;
        private set => SetProperty(ref _toastMessage, value);
    }

    public string ToastBackground
    {
        get => _toastBackground;
        private set => SetProperty(ref _toastBackground, value);
    }

    public string ToastForeground
    {
        get => _toastForeground;
        private set => SetProperty(ref _toastForeground, value);
    }

    public CommunityToolkit.Mvvm.Input.IRelayCommand<NavigationItemViewModel?> SelectSectionCommand { get; }

    public CommunityToolkit.Mvvm.Input.IRelayCommand SyncCommand { get; }

    public void ApplyPresetRange(ActivityRangePreset preset)
    {
        if (preset is ActivityRangePreset.Custom)
        {
            return;
        }

        ApplyRangeSelection(new ActivityRangeSelection(preset), persist: true);
        ReloadActivitySections();
    }

    public void ApplyCustomRange(DateTime startDate, DateTime endDate)
    {
        ApplyRangeSelection(new ActivityRangeSelection(ActivityRangePreset.Custom, startDate.Date, endDate.Date), persist: true);
        ReloadActivitySections();
    }

    public void RestoreCurrentRangeSelection()
    {
        UpdateRangeOptionLabels();
        SelectedRangeOption = GetRangeOption(_currentRangeSelection.Preset);
    }

    public (DateTime StartDate, DateTime EndDate) GetCustomRangeDraft()
    {
        if (_currentRangeSelection.Preset is ActivityRangePreset.Custom &&
            _currentRangeSelection.StartDate is { } startDate &&
            _currentRangeSelection.EndDate is { } endDate)
        {
            return (startDate, endDate);
        }

        var today = DateTime.Today;
        return (today.AddDays(-29), today);
    }

    public async System.Threading.Tasks.Task StartFileImportAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        SelectSection(_importNavigationItem);

        var fileName = System.IO.Path.GetFileName(filePath);

        if (!TryStartPipelineTask(
                PipelineTaskKind.Import,
                "Import pliku treningowego",
                $"Przygotowanie {fileName} do zapisu w SQLite.",
                12))
        {
            _importViewModel.SetImportBlocked("Poczekaj na zakonczenie biezacego zadania i sprobuj ponownie.");
            return;
        }

        _importViewModel.SetImportQueued(filePath);

        UpdateStatus(
            "Import",
            $"Rozpoczeto import pliku {fileName}.",
            "Dolny loader jest aktywny tylko dla tego importu.",
            "#3A1B47",
            "#FF9AE3");

        await RunImportPreviewAsync(filePath);
    }

    public async Task ConnectStravaAsync()
    {
        SelectSection(_importNavigationItem);

        if (!TryStartPipelineTask(
                PipelineTaskKind.Sync,
                "Laczenie ze Strava",
                "Zapisuje dane aplikacji Strava i otwieram przegladarke do autoryzacji OAuth.",
                8))
        {
            return;
        }

        _importViewModel.SetStravaBusyState(true);

        try
        {
            await AdvanceTaskStepAsync(PipelineTaskKind.Sync, "Zapisywanie danych aplikacji Strava.", 16, 140);

            await _stravaIntegrationService.SaveManualConfigurationAsync(
                new VeloCenter.Core.Integrations.StravaManualConfiguration(
                    _importViewModel.StravaClientId,
                    _importViewModel.StravaClientSecret));
            await AdvanceTaskStepAsync(PipelineTaskKind.Sync, "Otwieram Strave w przegladarce.", 34, 140);

            var state = await _stravaIntegrationService.ConnectAsync();
            _importViewModel.SetStravaState(state);
            _importViewModel.ClearStravaCredentials();
            await AdvanceTaskStepAsync(PipelineTaskKind.Sync, "Autoryzacja Stravy zakonczona i zapisana lokalnie.", 100, 160);

            _ = ShowToastAsync("Polaczono ze Strava", "#20342C", "#F3FFF8");

            UpdateStatus(
                "Done",
                "Strava zostala polaczona lokalnie.",
                "Mozesz rozpoczac synchronizacje aktywnosci rowerowych outdoor.",
                "#2A1734",
                "#F7E9FF");
        }
        catch (Exception exception)
        {
            var message = GetStravaErrorMessage(exception);

            _importViewModel.SetStravaError(message);
            _ = ShowToastAsync("Nie udalo sie polaczyc ze Strava", "#4A1D28", "#FFD9E1");

            UpdateStatus(
                "Error",
                "Laczenie ze Strava nie powiodlo sie.",
                message,
                "#4A1D28",
                "#FFD9E1");
        }
        finally
        {
            _importViewModel.SetStravaBusyState(false);
            RefreshStravaState();
            await Task.Delay(200);
            SetCurrentPipelineTask(PipelineTaskKind.None);
            SetTaskMonitorIdle("Loader uruchomi sie przy imporcie pliku albo synchronizacji Strava.");
        }
    }

    public async Task DisconnectStravaAsync()
    {
        SelectSection(_importNavigationItem);

        if (!TryStartPipelineTask(
                PipelineTaskKind.Sync,
                "Rozlaczanie Stravy",
                "Usuwam lokalna konfiguracje i sesje Stravy.",
                12))
        {
            return;
        }

        _importViewModel.SetStravaBusyState(true);

        try
        {
            await AdvanceTaskStepAsync(PipelineTaskKind.Sync, "Usuwanie lokalnej sesji Stravy.", 48, 120);
            var state = await _stravaIntegrationService.DisconnectAsync();
            _importViewModel.SetStravaState(state);
            _importViewModel.ClearStravaCredentials();
            await AdvanceTaskStepAsync(PipelineTaskKind.Sync, "Strava zostala rozlaczona.", 100, 120);

            _ = ShowToastAsync("Rozlaczono Strave", "#20342C", "#F3FFF8");

            UpdateStatus(
                "Done",
                "Strava zostala rozlaczona.",
                "Mozesz ponownie wpisac dane aplikacji i podlaczyc konto od nowa.",
                "#2A1734",
                "#F7E9FF");
        }
        catch (Exception exception)
        {
            var message = GetStravaErrorMessage(exception);

            _importViewModel.SetStravaError(message);
            _ = ShowToastAsync("Nie udalo sie rozlaczyc Stravy", "#4A1D28", "#FFD9E1");

            UpdateStatus(
                "Error",
                "Rozlaczenie Stravy nie powiodlo sie.",
                message,
                "#4A1D28",
                "#FFD9E1");
        }
        finally
        {
            _importViewModel.SetStravaBusyState(false);
            RefreshStravaState();
            await Task.Delay(200);
            SetCurrentPipelineTask(PipelineTaskKind.None);
            SetTaskMonitorIdle("Loader uruchomi sie przy imporcie pliku albo synchronizacji Strava.");
        }
    }

    public async Task RunStravaPrimaryActionAsync()
    {
        if (_stravaIntegrationService.GetConnectionState().IsConnected)
        {
            await SyncStravaAsync(navigateToIntegrations: true);
            return;
        }

        await ConnectStravaAsync();

        if (_stravaIntegrationService.GetConnectionState().IsConnected)
        {
            await SyncStravaAsync(navigateToIntegrations: true);
        }
    }

    public async Task ResetApplicationAsync()
    {
        if (!TryStartPipelineTask(
                PipelineTaskKind.Maintenance,
                "Reset aplikacji",
                "Usuwam lokalna baze, sesje integracji i dane konfiguracyjne.",
                10))
        {
            return;
        }

        try
        {
            await AdvanceTaskStepAsync(PipelineTaskKind.Maintenance, "Zamykanie lokalnych polaczen i przygotowanie resetu.", 36, 130);
            _applicationResetService.ResetAllData();
            _importViewModel.ResetViewState();
            ApplyRangeSelection(ActivityRangeSelection.Default, persist: false);
            ReloadActivitySections();
            RefreshStravaState();
            SelectSection(_importNavigationItem);
            await AdvanceTaskStepAsync(PipelineTaskKind.Maintenance, "Aplikacja zostala wyzerowana do pustego stanu.", 100, 150);

            _ = ShowToastAsync("Wyczyszczono wszystkie dane aplikacji", "#20342C", "#F3FFF8");

            UpdateStatus(
                "Done",
                "Aplikacja zostala calkowicie wyczyszczona.",
                "Baza, sesje integracji i lokalny stan zostaly usuniete.",
                "#2A1734",
                "#F7E9FF");
        }
        catch (Exception exception)
        {
            _ = ShowToastAsync("Nie udalo sie wyczyscic aplikacji", "#4A1D28", "#FFD9E1");

            UpdateStatus(
                "Error",
                "Reset aplikacji nie powiodl sie.",
                exception.Message,
                "#4A1D28",
                "#FFD9E1");
        }
        finally
        {
            await Task.Delay(200);
            SetCurrentPipelineTask(PipelineTaskKind.None);
            SetTaskMonitorIdle("Loader uruchomi sie przy imporcie pliku albo synchronizacji Strava.");
        }
    }

    public async Task SyncStravaAsync(bool navigateToIntegrations = true)
    {
        if (navigateToIntegrations)
        {
            SelectSection(_importNavigationItem);
        }

        if (!TryStartPipelineTask(
                PipelineTaskKind.Sync,
                "Synchronizacja ze Strava",
                "Pobieram partiami tylko aktywnosci rowerowe outdoor i zapisuje je do SQLite.",
                8))
        {
            return;
        }

        _importViewModel.SetStravaBusyState(true);

        try
        {
            var progress = new Progress<VeloCenter.Core.Integrations.StravaSyncProgress>(progressUpdate =>
            {
                _importViewModel.SetStravaSyncProgress(progressUpdate);
                SetTaskMonitor(
                    "Synchronizacja ze Strava",
                    progressUpdate.Message,
                    progressUpdate.ProgressHint);
            });

            var result = await _stravaIntegrationService.SyncActivitiesAsync(progress);
            ReloadActivitySections();
            RefreshStravaState();
            await AdvanceTaskStepAsync(PipelineTaskKind.Sync, "Synchronizacja Stravy zakonczona.", 100, 140);

            _ = ShowToastAsync(
                result.CreatedActivities > 0 || result.UpdatedActivities > 0
                    ? $"Strava: {result.MatchedActivities} outdoor, +{result.CreatedActivities} nowych, {result.UpdatedActivities} zaktualizowanych"
                    : result.SkippedActivities > 0
                        ? $"Strava: pominieto {result.SkippedActivities} aktywnosci spoza roweru outdoor"
                        : "Strava: brak nowych aktywnosci rowerowych outdoor",
                "#20342C",
                "#F3FFF8");

            UpdateStatus(
                "Done",
                "Synchronizacja ze Strava zakonczona.",
                $"Przejrzano {result.ProcessedActivities} aktywnosci, dopasowano {result.MatchedActivities}, pominieto {result.SkippedActivities} w {result.PagesFetched} paczkach.",
                "#2A1734",
                "#F7E9FF");
        }
        catch (Exception exception)
        {
            var message = GetStravaErrorMessage(exception);

            _importViewModel.SetStravaError(message);
            _ = ShowToastAsync("Synchronizacja Stravy nie powiodla sie", "#4A1D28", "#FFD9E1");

            UpdateStatus(
                "Error",
                "Synchronizacja ze Strava nie powiodla sie.",
                message,
                "#4A1D28",
                "#FFD9E1");
        }
        finally
        {
            _importViewModel.SetStravaBusyState(false);
            RefreshStravaState();
            await Task.Delay(200);
            SetCurrentPipelineTask(PipelineTaskKind.None);
            SetTaskMonitorIdle("Loader uruchomi sie przy imporcie pliku albo synchronizacji Strava.");
        }
    }

    public void SetSidebarHoverState(bool isHovered)
    {
        _sidebarTransitionVersion++;
        var version = _sidebarTransitionVersion;

        if (isHovered)
        {
            IsSidebarContentExpanded = true;
            IsSidebarExpanded = true;
            return;
        }

        IsSidebarExpanded = false;
        _ = CompleteSidebarCollapseAsync(version);
    }

    private void SelectSection(NavigationItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsSelected = object.ReferenceEquals(navigationItem, item);
        }

        _currentSectionKey = item.Key;
        OnPropertyChanged(nameof(ShowTopBarRangeSelector));
        OnPropertyChanged(nameof(UseViewportSectionLayout));
        CurrentSectionTitle = item.Title;
        CurrentSectionDescription = item.Description;
        CurrentSectionViewModel = ResolveSection(item.Key);

        UpdateStatus(
            "Ready",
            $"Otwarto sekcje {item.Title.ToLowerInvariant()}.",
            $"Pod reka jest teraz widok: {item.Description.ToLowerInvariant()}",
            "#2A1734",
            "#F7E9FF");
    }

    private ViewModelBase ResolveSection(string key) => key switch
    {
        "overview" => _overviewViewModel,
        "workouts" => _workoutsViewModel,
        "progress" => _progressViewModel,
        "import" => _importViewModel,
        "settings" => _settingsViewModel,
        _ => _overviewViewModel,
    };

    private void OpenImportWorkspace()
    {
        SelectSection(_importNavigationItem);

        UpdateStatus(
            "Ready",
            "Sekcja integracji czeka na wskazanie lokalnego pliku.",
            "Dolny loader nie pojawi sie, dopoki nie rozpoczniesz importu albo synchronizacji.",
            "#241D44",
            "#C9C3FF");
    }

    private async Task RunToolbarSyncAsync()
    {
        if (_stravaIntegrationService.GetConnectionState().IsConnected)
        {
            await SyncStravaAsync(navigateToIntegrations: false);
            return;
        }

        UpdateStatus(
            "Manual",
            "Brak polaczonych integracji do odswiezenia.",
            "Polacz Strave w sekcji integracji, aby przycisk odswiezania uruchamial synchronizacje.",
            "#301A44",
            "#F1B2FF");
    }

    private void RefreshCurrentSection()
    {
        UpdateStatus(
            "Updated",
            $"Odswiezono placeholder dla sekcji {CurrentSectionTitle.ToLowerInvariant()}.",
            "Dolny loader pozostaje przypiety do realnych operacji integracji.",
            "#301A44",
            "#F1B2FF");
    }

    private void ApplySidebarStateToNavigationItems()
    {
        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsExpanded = IsSidebarContentExpanded;
        }
    }

    private int _sidebarTransitionVersion;

    private async System.Threading.Tasks.Task CompleteSidebarCollapseAsync(int version)
    {
        await System.Threading.Tasks.Task.Delay(180);

        if (version != _sidebarTransitionVersion || IsSidebarExpanded)
        {
            return;
        }

        IsSidebarContentExpanded = false;
    }

    private bool TryStartPipelineTask(
        PipelineTaskKind taskKind,
        string title,
        string detail,
        double progress)
    {
        if (_currentPipelineTask is not PipelineTaskKind.None)
        {
            UpdateStatus(
                "Busy",
                "Pipeline jest juz zajety przez inne zadanie.",
                "Poczekaj na zakonczenie biezacego importu albo synchronizacji, zanim uruchomisz kolejne zadanie.",
                "#3A1B47",
                "#FF9AE3");
            return false;
        }

        SetCurrentPipelineTask(taskKind);
        SetTaskMonitor(title, detail, progress);
        return true;
    }

    private async System.Threading.Tasks.Task RunImportPreviewAsync(string filePath)
    {
        var fileName = System.IO.Path.GetFileName(filePath);

        try
        {
            await AdvanceImportStepAsync("Sprawdzanie formatu pliku.", 18, 180);
            await AdvanceImportStepAsync("Odczytywanie danych aktywnosci.", 44, 220);

            var result = _activityImportService.ImportLocalFile(filePath);
            ReloadActivitySections();

            await AdvanceImportStepAsync(
                result.WasCreated
                    ? "Aktywnosc zostala zapisana w SQLite."
                    : "Istniejaca aktywnosc zostala zaktualizowana w SQLite.",
                73,
                220);
            await AdvanceImportStepAsync("Odswiezanie widokow aplikacji.", 100, 200);

            _importViewModel.SetImportCompleted(result);
            _ = ShowToastAsync(
                result.WasCreated
                    ? $"Zaimportowano {result.Activity.Title}"
                    : $"Zaktualizowano {result.Activity.Title}",
                "#20342C",
                "#F3FFF8");

            UpdateStatus(
                "Done",
                result.WasCreated
                    ? $"Plik {fileName} zostal zapisany w bazie."
                    : $"Plik {fileName} zaktualizowal istniejacy wpis w bazie.",
                "Aktywnosc jest juz dostepna w pozostalych sekcjach aplikacji.",
                "#2A1734",
                "#F7E9FF");
        }
        catch (Exception exception)
        {
            var errorMessage = GetImportErrorMessage(exception);

            _importViewModel.SetImportFailed(errorMessage);
            _ = ShowToastAsync("Import nie powiodl sie", "#4A1D28", "#FFD9E1");

            UpdateStatus(
                "Error",
                $"Import pliku {fileName} nie powiodl sie.",
                errorMessage,
                "#4A1D28",
                "#FFD9E1");
        }
        finally
        {
            await System.Threading.Tasks.Task.Delay(200);
            SetCurrentPipelineTask(PipelineTaskKind.None);
            SetTaskMonitorIdle("Loader uruchomi sie przy imporcie pliku albo synchronizacji Strava.");
        }
    }

    private async System.Threading.Tasks.Task AdvanceImportStepAsync(
        string detail,
        double progress,
        int delayMs)
        => await AdvanceTaskStepAsync(PipelineTaskKind.Import, detail, progress, delayMs);

    private async System.Threading.Tasks.Task AdvanceTaskStepAsync(
        PipelineTaskKind taskKind,
        string detail,
        double progress,
        int delayMs)
    {
        var startProgress = TaskProgressValue;
        var steps = Math.Max(1, delayMs / 24);
        var title = taskKind switch
        {
            PipelineTaskKind.Sync => "Synchronizacja ze Strava",
            PipelineTaskKind.Maintenance => "Reset aplikacji",
            _ => "Import pliku treningowego",
        };

        for (var step = 1; step <= steps; step++)
        {
            var interpolatedProgress = startProgress + ((progress - startProgress) * step / steps);

            SetTaskMonitor(title, detail, interpolatedProgress);
            await System.Threading.Tasks.Task.Delay(Math.Max(16, delayMs / steps));
        }
    }

    private void SetTaskMonitorIdle(string detail)
    {
        SetTaskMonitor("Brak aktywnego zadania", detail, 0);
    }

    private void SetTaskMonitor(
        string title,
        string detail,
        double progress)
    {
        TaskTitle = title;
        TaskDetail = detail;
        TaskProgressValue = progress;
    }

    private void SetCurrentPipelineTask(PipelineTaskKind taskKind)
    {
        if (_currentPipelineTask == taskKind)
        {
            return;
        }

        _currentPipelineTask = taskKind;
        OnPropertyChanged(nameof(IsTaskMonitorVisible));
    }

    private async System.Threading.Tasks.Task ShowToastAsync(
        string message,
        string background,
        string foreground)
    {
        var version = ++_toastVersion;

        ToastMessage = message;
        ToastBackground = background;
        ToastForeground = foreground;
        IsToastVisible = true;

        await System.Threading.Tasks.Task.Delay(2600);

        if (version != _toastVersion)
        {
            return;
        }

        IsToastVisible = false;
    }

    private void ReloadActivitySections()
    {
        var activities = _activityRepository.GetRecentActivities();

        _allActivities = activities;
        _hasActivities = activities.Count > 0;
        _overviewViewModel = new OverviewViewModel(VeloCenter.Core.Activities.TrainingOverview.FromActivities(activities), activities);
        _workoutsViewModel = new WorkoutsViewModel(GetVisibleWorkoutsActivities(), activities.Count, CurrentRangeLabel);
        _progressViewModel = new ProgressViewModel(activities);

        var selectedNavigationItem = NavigationItems.FirstOrDefault(item => item.IsSelected);

        if (selectedNavigationItem is not null)
        {
            CurrentSectionViewModel = ResolveSection(selectedNavigationItem.Key);
        }
    }

    private IReadOnlyList<VeloCenter.Core.Activities.ActivitySummary> GetVisibleWorkoutsActivities()
    {
        if (_currentRangeSelection.Preset is ActivityRangePreset.All)
        {
            return _allActivities;
        }

        var (startDate, endDate) = GetCurrentRangeBounds();

        return
        [
            .. _allActivities.Where(activity =>
            {
                var activityDate = activity.StartTime.ToLocalTime().Date;
                return activityDate >= startDate && activityDate <= endDate;
            }),
        ];
    }

    private (DateTime StartDate, DateTime EndDate) GetCurrentRangeBounds()
    {
        var today = DateTime.Today;

        return _currentRangeSelection.Preset switch
        {
            ActivityRangePreset.Last30Days => (today.AddDays(-29), today),
            ActivityRangePreset.ThisMonth => (new DateTime(today.Year, today.Month, 1), today),
            ActivityRangePreset.ThisYear => (new DateTime(today.Year, 1, 1), today),
            ActivityRangePreset.Custom when _currentRangeSelection.StartDate is { } startDate &&
                _currentRangeSelection.EndDate is { } endDate => (startDate.Date, endDate.Date),
            _ => (today.AddDays(-29), today),
        };
    }

    private void ApplyRangeSelection(ActivityRangeSelection selection, bool persist)
    {
        _currentRangeSelection = NormalizeRangeSelection(selection);
        UpdateRangeOptionLabels();
        SelectedRangeOption = GetRangeOption(_currentRangeSelection.Preset);

        if (persist)
        {
            _activityRangePreferencesStore.Save(_currentRangeSelection);
        }
    }

    private static ActivityRangeSelection NormalizeRangeSelection(ActivityRangeSelection selection)
    {
        if (selection.Preset is not ActivityRangePreset.Custom)
        {
            return new ActivityRangeSelection(selection.Preset);
        }

        if (selection.StartDate is null || selection.EndDate is null)
        {
            return ActivityRangeSelection.Default;
        }

        var startDate = selection.StartDate.Value.Date;
        var endDate = selection.EndDate.Value.Date;

        if (endDate < startDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        return new ActivityRangeSelection(ActivityRangePreset.Custom, startDate, endDate);
    }

    private void UpdateRangeOptionLabels()
    {
        _last30DaysRangeOption.Label = "Ostatnie 30 dni";
        _thisMonthRangeOption.Label = "Ten miesiac";
        _thisYearRangeOption.Label = "Ten rok";
        _allRangeOption.Label = "Wszystkie";
        _customRangeOption.Label = _currentRangeSelection.Preset is ActivityRangePreset.Custom &&
            _currentRangeSelection.StartDate is { } startDate &&
            _currentRangeSelection.EndDate is { } endDate
            ? $"{startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}"
            : "Zakres od-do";
    }

    private ActivityRangeOptionViewModel GetRangeOption(ActivityRangePreset preset) => preset switch
    {
        ActivityRangePreset.Last30Days => _last30DaysRangeOption,
        ActivityRangePreset.ThisMonth => _thisMonthRangeOption,
        ActivityRangePreset.ThisYear => _thisYearRangeOption,
        ActivityRangePreset.Custom => _customRangeOption,
        ActivityRangePreset.All => _allRangeOption,
        _ => _last30DaysRangeOption,
    };

    private void RefreshStravaState()
    {
        _importViewModel.SetStravaState(_stravaIntegrationService.GetConnectionState());
    }

    private static string GetImportErrorMessage(Exception exception) => exception switch
    {
        FileNotFoundException => "Wybrany plik nie jest juz dostepny.",
        InvalidDataException => exception.Message,
        _ => "Nie udalo sie zapisac aktywnosci do lokalnej bazy.",
    };

    private static string GetStravaErrorMessage(Exception exception) => exception switch
    {
        TimeoutException => "Przekroczono czas oczekiwania na autoryzacje Strava.",
        InvalidOperationException => exception.Message,
        System.Net.HttpListenerException => "Nie udalo sie uruchomic lokalnego callbacku. Sprawdz, czy callback domain w Stravie to 127.0.0.1.",
        HttpRequestException => "Strava odrzucila zadanie. Sprawdz Client ID, Client Secret i polaczenie z internetem.",
        _ => "Wystapil nieoczekiwany blad integracji ze Strava.",
    };

    private static string ResolveApplicationVersion()
    {
        var version = System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(
                System.Reflection.Assembly.GetExecutingAssembly())?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(version))
        {
            return "0.1.0";
        }

        return version.Split('+')[0];
    }

    private void UpdateStatus(
        string state,
        string message,
        string activeTask,
        string badgeBackground,
        string badgeForeground)
    {
        StatusState = state;
        StatusMessage = message;
        ActiveTask = activeTask;
        StatusBadgeBackground = badgeBackground;
        StatusBadgeForeground = badgeForeground;
        LastActionLabel = $"Ostatnia akcja {System.DateTimeOffset.Now:HH:mm}";
    }
}
