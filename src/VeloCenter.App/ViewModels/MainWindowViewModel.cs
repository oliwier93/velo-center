namespace VeloCenter.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private enum PipelineTaskKind
    {
        None,
        Import,
        Sync,
    }

    private readonly OverviewViewModel _overviewViewModel;
    private readonly WorkoutsViewModel _workoutsViewModel;
    private readonly ProgressViewModel _progressViewModel;
    private readonly ImportViewModel _importViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly NavigationItemViewModel _overviewNavigationItem;
    private readonly NavigationItemViewModel _importNavigationItem;
    private readonly Avalonia.Threading.DispatcherTimer _taskStripeTimer;
    private readonly System.Diagnostics.Stopwatch _taskStripeStopwatch;
    private readonly bool _hasActivities;

    private bool _isSidebarExpanded;
    private bool _isSidebarContentExpanded;
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

    public MainWindowViewModel()
        : this(new VeloCenter.Infrastructure.Activities.InMemoryActivityRepository())
    {
    }

    public MainWindowViewModel(VeloCenter.Core.Activities.IActivityRepository activityRepository)
    {
        var activities = activityRepository.GetRecentActivities();
        _hasActivities = activities.Count > 0;

        _overviewViewModel = new OverviewViewModel(VeloCenter.Core.Activities.TrainingOverview.FromActivities(activities), activities);
        _workoutsViewModel = new WorkoutsViewModel(activities);
        _progressViewModel = new ProgressViewModel(activities);
        _importViewModel = new ImportViewModel();
        _settingsViewModel = new SettingsViewModel();

        _overviewNavigationItem = new NavigationItemViewModel(
            "overview",
            "Przeglad",
            "Szybki obraz tygodnia, obciazenia i ostatnich przejazdow.",
            "M5,5 H11 V11 H5 Z M13,5 H19 V9 H13 Z M5,13 H10 V19 H5 Z M12,12 H19 V19 H12 Z");
        var workoutsNavigationItem = new NavigationItemViewModel(
            "workouts",
            "Treningi",
            "Biblioteka przejazdow, filtry i podglad danych z aktywnosci.",
            "M4,16 C7,11 10,11 13,14 C15,16 17,16 20,8 M5.5,16 A1.5,1.5 0 1 0 5.6,16 M13,14 A1.5,1.5 0 1 0 13.1,14 M20,8 A1.5,1.5 0 1 0 20.1,8");
        var progressNavigationItem = new NavigationItemViewModel(
            "progress",
            "Postep",
            "Trendy tygodniowe, sygnaly i kolejne metryki rozwojowe.",
            "M5,17 L10,12 L14,14 L19,7 M15,7 H19 V11");
        _importNavigationItem = new NavigationItemViewModel(
            "import",
            "Import",
            "Pliki FIT i GPX, synchronizacja oraz kolejka zadan.",
            "M12,4 V13 M8,10 L12,14 L16,10 M5,18 H19 V20 H5 Z");
        var settingsNavigationItem = new NavigationItemViewModel(
            "settings",
            "Ustawienia",
            "Konfiguracja projektu, storage i preferencji interfejsu.",
            "M6,7 H18 M6,12 H18 M6,17 H18 M9,7 A2,2 0 1 0 9.1,7 M15,12 A2,2 0 1 0 15.1,12 M11,17 A2,2 0 1 0 11.1,17");

        NavigationItems =
        [
            _overviewNavigationItem,
            workoutsNavigationItem,
            progressNavigationItem,
            _importNavigationItem,
            settingsNavigationItem,
        ];

        SelectSectionCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<NavigationItemViewModel?>(SelectSection);
        SyncCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(RunToolbarSync);

        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.ActivateCommand = SelectSectionCommand;
        }

        ApplySidebarStateToNavigationItems();
        SelectSection(_hasActivities ? _overviewNavigationItem : _importNavigationItem);
        SetTaskMonitorIdle("Loader uruchomi sie dopiero po wybraniu pliku FIT albo GPX.");

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
                : "Baza jest pusta. Zacznij od wyboru lokalnego pliku do importu.",
            _hasActivities
                ? "Brak zadania w tle."
                : "Loader pozostaje ukryty, dopoki nie wybierzesz pliku.",
            "#2A1734",
            "#F7E9FF");
    }

    public string AppTitle { get; } = "Velo Center";

    public string AppVersionLabel { get; } = $"v{ResolveApplicationVersion()}";

    public string AppAuthorLabel { get; } = "Oliwier Baran";

    public string AppMetaLabel => $"{AppVersionLabel}  •  {AppAuthorLabel}";

    public string CurrentRangeLabel { get; } = "30 dni";

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

    public System.Collections.Generic.IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

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

    public CommunityToolkit.Mvvm.Input.IRelayCommand<NavigationItemViewModel?> SelectSectionCommand { get; }

    public CommunityToolkit.Mvvm.Input.IRelayCommand SyncCommand { get; }

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
                $"Przygotowanie {fileName} do przyszlego parsera i zapisu w SQLite.",
                12))
        {
            _importViewModel.SetImportBlocked("Poczekaj na zakonczenie biezacego zadania i sprobuj ponownie.");
            return;
        }

        _importViewModel.SetSelectedFile(filePath);

        UpdateStatus(
            "Import",
            $"Rozpoczeto testowe wczytywanie pliku {fileName}.",
            "Dolny loader jest aktywny tylko dla tego importu.",
            "#3A1B47",
            "#FF9AE3");

        await RunImportPreviewAsync(filePath);
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
            "Sekcja importu czeka na wskazanie lokalnego pliku.",
            "Dolny loader nie pojawi sie, dopoki nie rozpoczniesz importu.",
            "#241D44",
            "#C9C3FF");
    }

    private void RunToolbarSync()
    {
        UpdateStatus(
            "Manual",
            "Przycisk odswiezania jest chwilowo odlaczony od dolnego loadera.",
            "Loader reaguje teraz tylko na import lokalnego pliku.",
            "#301A44",
            "#F1B2FF");
    }

    private void RefreshCurrentSection()
    {
        UpdateStatus(
            "Updated",
            $"Odswiezono placeholder dla sekcji {CurrentSectionTitle.ToLowerInvariant()}.",
            "Dolny loader pozostaje przypiety tylko do importu pliku.",
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
                "Poczekaj na zakonczenie biezacego importu, zanim uruchomisz kolejny.",
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
            await AdvanceImportStepAsync("Sprawdzanie rozszerzenia i przygotowanie loadera.", 28, 220);
            await AdvanceImportStepAsync("Plik trafia do testowego przeplywu UI bez zapisu do bazy.", 57, 260);
            await AdvanceImportStepAsync("Walidacja pustego stanu i odswiezenie kontrolek importu.", 86, 240);
            await AdvanceImportStepAsync("Loader zakonczyl przebieg testowy.", 100, 180);

            _importViewModel.SetImportCompleted(filePath);

            UpdateStatus(
                "Done",
                $"Plik {fileName} przeszedl przez frontendowy loader.",
                "Na razie to test UI bez parsera i bez zapisu do SQLite.",
                "#2A1734",
                "#F7E9FF");
        }
        finally
        {
            await System.Threading.Tasks.Task.Delay(200);
            SetCurrentPipelineTask(PipelineTaskKind.None);
            SetTaskMonitorIdle("Loader uruchomi sie ponownie przy kolejnym imporcie pliku.");
        }
    }

    private async System.Threading.Tasks.Task AdvanceImportStepAsync(
        string detail,
        double progress,
        int delayMs)
    {
        SetTaskMonitor("Import pliku treningowego", detail, progress);
        await System.Threading.Tasks.Task.Delay(delayMs);
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
