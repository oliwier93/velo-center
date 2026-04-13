namespace VeloCenter.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly OverviewViewModel _overviewViewModel;
    private readonly WorkoutsViewModel _workoutsViewModel;
    private readonly ProgressViewModel _progressViewModel;
    private readonly ImportViewModel _importViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly NavigationItemViewModel _overviewNavigationItem;
    private readonly NavigationItemViewModel _importNavigationItem;

    private bool _isSidebarExpanded = true;
    private string _currentSectionTitle = string.Empty;
    private string _currentSectionDescription = string.Empty;
    private ViewModelBase _currentSectionViewModel = null!;
    private string _statusState = string.Empty;
    private string _statusMessage = string.Empty;
    private string _activeTask = string.Empty;
    private string _lastActionLabel = string.Empty;
    private string _statusBadgeBackground = "#2A1734";
    private string _statusBadgeForeground = "#F7E9FF";

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

        ToggleSidebarCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ToggleSidebar);
        SelectSectionCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<NavigationItemViewModel?>(SelectSection);
        ImportCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(OpenImportWorkspace);
        SyncCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(StartSyncPlaceholder);
        RefreshCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(RefreshCurrentSection);

        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.ActivateCommand = SelectSectionCommand;
        }

        ApplySidebarStateToNavigationItems();
        SelectSection(_overviewNavigationItem);
        UpdateStatus(
            "Ready",
            "Nowy shell zaladowal probne aktywnosci i gotowe sekcje aplikacji.",
            "Brak zadania w tle.",
            "#2A1734",
            "#F7E9FF");
    }

    public string AppTitle { get; } = "velo-center";

    public string CurrentRangeLabel { get; } = "Zakres: 30 dni";

    public string DataModeLabel { get; } = "Tryb lokalny / probka";

    public string SidebarFooterTitle => IsSidebarExpanded ? "Tryb lokalny" : "LCL";

    public string SidebarFooterDescription => IsSidebarExpanded
        ? "Shell pracuje na probce in-memory, dopoki nie podepniemy SQLite i importu plikow."
        : string.Empty;

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        private set
        {
            if (SetProperty(ref _isSidebarExpanded, value))
            {
                OnPropertyChanged(nameof(SidebarWidth));
                OnPropertyChanged(nameof(SidebarToggleIconPathData));
                OnPropertyChanged(nameof(SidebarToggleTooltip));
                OnPropertyChanged(nameof(SidebarFooterTitle));
                OnPropertyChanged(nameof(SidebarFooterDescription));
                ApplySidebarStateToNavigationItems();
            }
        }
    }

    public double SidebarWidth => IsSidebarExpanded ? 264 : 92;

    public string SidebarToggleIconPathData => IsSidebarExpanded
        ? "M15.5,6 L9.5,12 L15.5,18"
        : "M8.5,6 L14.5,12 L8.5,18";

    public string SidebarToggleTooltip => IsSidebarExpanded ? "Zwin menu" : "Rozwin menu";

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

    public CommunityToolkit.Mvvm.Input.IRelayCommand ToggleSidebarCommand { get; }

    public CommunityToolkit.Mvvm.Input.IRelayCommand<NavigationItemViewModel?> SelectSectionCommand { get; }

    public CommunityToolkit.Mvvm.Input.IRelayCommand ImportCommand { get; }

    public CommunityToolkit.Mvvm.Input.IRelayCommand SyncCommand { get; }

    public CommunityToolkit.Mvvm.Input.IRelayCommand RefreshCommand { get; }

    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;

        UpdateStatus(
            "Layout",
            IsSidebarExpanded ? "Menu boczne zostalo rozwiniete." : "Menu boczne zostalo zwiniete.",
            "Interfejs czeka na kolejne akcje.",
            "#26183D",
            "#CDB5FF");
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
            "Queued",
            "Otworzono przestrzen importu dla FIT, GPX i przyszlego syncu.",
            "Nastepny krok: podpiecie realnego flow importu plikow.",
            "#3A1B47",
            "#FF9AE3");
    }

    private void StartSyncPlaceholder()
    {
        UpdateStatus(
            "Pending",
            "Synchronizacja jest na razie placeholderem warstwy shell.",
            "Do dowiezienia: OAuth, kolejka syncu i mapowanie aktywnosci.",
            "#241D44",
            "#C9C3FF");
    }

    private void RefreshCurrentSection()
    {
        UpdateStatus(
            "Updated",
            $"Odswiezono placeholder dla sekcji {CurrentSectionTitle.ToLowerInvariant()}.",
            "Brak zadania w tle.",
            "#301A44",
            "#F1B2FF");
    }

    private void ApplySidebarStateToNavigationItems()
    {
        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsExpanded = IsSidebarExpanded;
        }
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
