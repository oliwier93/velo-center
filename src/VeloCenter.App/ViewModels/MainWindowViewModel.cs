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
        SelectSection(_overviewNavigationItem);
        SetTaskMonitor("Import FIT / GPX", "Mock postepu do dopracowania UI.", 64);

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
            "Ready",
            "Nowy shell zaladowal probne aktywnosci i gotowe sekcje aplikacji.",
            "Brak zadania w tle.",
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

    public CommunityToolkit.Mvvm.Input.IRelayCommand<NavigationItemViewModel?> SelectSectionCommand { get; }

    public CommunityToolkit.Mvvm.Input.IRelayCommand SyncCommand { get; }

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

        if (!TryStartPipelineTask(
                PipelineTaskKind.Import,
                "Import FIT / GPX",
                "Czytanie plikow, mapowanie aktywnosci i zapis do lokalnej bazy. Synchronizacja czeka na wolny pipeline.",
                28))
        {
            return;
        }

        UpdateStatus(
            "Queued",
            "Otworzono przestrzen importu dla FIT, GPX i przyszlego syncu.",
            "Nastepny krok: podpiecie realnego flow importu plikow.",
            "#3A1B47",
            "#FF9AE3");
    }

    private void StartSyncPlaceholder()
    {
        if (!TryStartPipelineTask(
                PipelineTaskKind.Sync,
                "Synchronizacja z API",
                "Pobieranie zmian, uzgadnianie aktywnosci i odswiezanie lokalnych danych. Import poczeka na zakonczenie syncu.",
                34))
        {
            return;
        }

        UpdateStatus(
            "Pending",
            "Synchronizacja jest na razie placeholderem warstwy shell.",
            "Do dowiezienia: OAuth, kolejka syncu i mapowanie aktywnosci.",
            "#241D44",
            "#C9C3FF");
    }

    private void RunToolbarSync()
    {
        if (_currentPipelineTask is PipelineTaskKind.Sync)
        {
            AdvancePipelineTask();
            UpdateStatus(
                "Updated",
                "Odswiezono postep biezacej synchronizacji.",
                "Synchronizacja jest dalej w toku.",
                "#301A44",
                "#F1B2FF");
            return;
        }

        StartSyncPlaceholder();
    }

    private void RefreshCurrentSection()
    {
        AdvancePipelineTask();
        UpdateStatus(
            "Updated",
            $"Odswiezono placeholder dla sekcji {CurrentSectionTitle.ToLowerInvariant()}.",
            _currentPipelineTask is PipelineTaskKind.None
                ? "Brak zadania w tle."
                : "Biezace zadanie posunelo sie o kolejny krok pipeline.",
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
        if (_currentPipelineTask is not PipelineTaskKind.None && _currentPipelineTask != taskKind)
        {
            UpdateStatus(
                "Busy",
                "Pipeline jest juz zajety przez inne zadanie.",
                "Import i synchronizacja nie uruchamiaja sie rownoczesnie.",
                "#3A1B47",
                "#FF9AE3");
            return false;
        }

        _currentPipelineTask = taskKind;
        SetTaskMonitor(title, detail, progress);
        return true;
    }

    private void AdvancePipelineTask()
    {
        if (_currentPipelineTask is PipelineTaskKind.None)
        {
            SetTaskMonitorIdle("Pipeline czeka na import albo synchronizacje.");
            return;
        }

        var nextProgress = System.Math.Min(100, TaskProgressValue + 18);
        var taskIsImport = _currentPipelineTask is PipelineTaskKind.Import;
        var title = taskIsImport ? "Import FIT / GPX" : "Synchronizacja z API";

        if (nextProgress >= 100)
        {
            _currentPipelineTask = PipelineTaskKind.None;
            SetTaskMonitorIdle(
                taskIsImport
                    ? "Import zakonczony. Pipeline jest wolny i gotowy na synchronizacje."
                    : "Synchronizacja zakonczona. Mozesz odpalic kolejny import albo analize.");
            UpdateStatus(
                "Done",
                taskIsImport ? "Import zakonczyl sie pomyslnie." : "Synchronizacja zakonczyl sie pomyslnie.",
                "Pipeline jest wolny.",
                "#2A1734",
                "#F7E9FF");
            return;
        }

        SetTaskMonitor(
            title,
            taskIsImport
                ? "Import trwa. Odczyt plikow i mapowanie aktywnosci sa w toku."
                : "Synchronizacja trwa. Uzgadnianie aktywnosci z lokalna baza jest w toku.",
            nextProgress);
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
