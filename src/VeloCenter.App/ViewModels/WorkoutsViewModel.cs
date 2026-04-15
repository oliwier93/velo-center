using CommunityToolkit.Mvvm.Input;
using VeloCenter.Core.Activities;

namespace VeloCenter.App.ViewModels;

public sealed class WorkoutsViewModel : ViewModelBase
{
    private const int PageSize = 10;

    private readonly IReadOnlyList<RecentRideViewModel> _allRideLibrary;
    private readonly RelayCommand _previousPageCommand;
    private readonly RelayCommand _nextPageCommand;
    private IReadOnlyList<RecentRideViewModel> _rideLibrary = [];
    private int _currentPage = 1;

    public WorkoutsViewModel(
        IReadOnlyList<ActivitySummary> visibleActivities,
        int totalActivitiesCount,
        string rangeLabel)
    {
        HasActivities = visibleActivities.Count > 0;

        Highlights = BuildHighlights(visibleActivities, totalActivitiesCount, rangeLabel);
        _allRideLibrary =
        [
            .. visibleActivities
                .OrderByDescending(activity => activity.StartTime)
                .Select(activity => new RecentRideViewModel(activity)),
        ];

        EmptyLibraryTitle = totalActivitiesCount > 0
            ? "Brak treningow w wybranym zakresie"
            : "Brak treningow w bibliotece";
        EmptyLibraryDescription = totalActivitiesCount > 0
            ? $"W zakresie {rangeLabel.ToLowerInvariant()} nie ma jeszcze zadnych aktywnosci. Zmien zakres dat, aby zobaczyc starsze treningi."
            : "Lista aktywnosci wypelni sie po pierwszym imporcie. Na razie ten widok pokazuje, jak aplikacja zachowuje sie na pustej bazie.";

        _previousPageCommand = new RelayCommand(GoToPreviousPage, () => CanGoPreviousPage);
        _nextPageCommand = new RelayCommand(GoToNextPage, () => CanGoNextPage);

        PreviousPageCommand = _previousPageCommand;
        NextPageCommand = _nextPageCommand;

        RefreshRideLibraryPage();
    }

    public bool HasActivities { get; }

    public bool HasNoActivities => !HasActivities;

    public IReadOnlyList<MetricTileViewModel> Highlights { get; }

    public string EmptyLibraryTitle { get; }

    public string EmptyLibraryDescription { get; }

    public IReadOnlyList<RecentRideViewModel> RideLibrary
    {
        get => _rideLibrary;
        private set => SetProperty(ref _rideLibrary, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(PaginationLabel));
            }
        }
    }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)_allRideLibrary.Count / PageSize));

    public bool HasPagination => _allRideLibrary.Count > PageSize;

    public bool CanGoPreviousPage => HasPagination && CurrentPage > 1;

    public bool CanGoNextPage => HasPagination && CurrentPage < TotalPages;

    public string PaginationLabel
    {
        get
        {
            if (!HasPagination)
            {
                return string.Empty;
            }

            var firstItemIndex = ((CurrentPage - 1) * PageSize) + 1;
            var lastItemIndex = Math.Min(CurrentPage * PageSize, _allRideLibrary.Count);

            return $"Strona {CurrentPage} z {TotalPages}  •  {firstItemIndex}-{lastItemIndex} z {_allRideLibrary.Count}";
        }
    }

    public IRelayCommand PreviousPageCommand { get; }

    public IRelayCommand NextPageCommand { get; }

    private static IReadOnlyList<MetricTileViewModel> BuildHighlights(
        IReadOnlyList<ActivitySummary> visibleActivities,
        int totalActivitiesCount,
        string rangeLabel)
    {
        if (visibleActivities.Count > 0)
        {
            var latestRide = visibleActivities.OrderByDescending(activity => activity.StartTime).First();
            var longestRide = visibleActivities.OrderByDescending(activity => activity.DistanceKm).First();
            var averageDistance = visibleActivities.Average(activity => activity.DistanceKm);

            return
            [
                new MetricTileViewModel("Treningi w zakresie", visibleActivities.Count.ToString(), $"Zakres: {rangeLabel.ToLowerInvariant()}."),
                new MetricTileViewModel("Najnowszy przejazd", latestRide.DistanceLabel, latestRide.Title),
                new MetricTileViewModel("Sredni dystans", $"{averageDistance:0.0} km", "Na aktywnosc w wybranym zakresie."),
                new MetricTileViewModel("Najdluzszy trening", longestRide.DistanceLabel, longestRide.Title),
            ];
        }

        if (totalActivitiesCount > 0)
        {
            return
            [
                new MetricTileViewModel("Treningi w zakresie", "0", $"Brak aktywnosci dla zakresu {rangeLabel.ToLowerInvariant()}."),
                new MetricTileViewModel("Najnowszy przejazd", "--", "Zmien zakres dat, aby zobaczyc treningi."),
                new MetricTileViewModel("Sredni dystans", "--", "Poza biezacym zakresem sa juz zapisane aktywnosci."),
                new MetricTileViewModel("Najdluzszy trening", "--", "Biblioteka jest pelna dopiero po rozszerzeniu zakresu."),
            ];
        }

        return
        [
            new MetricTileViewModel("Treningi w zakresie", "0", "Biblioteka czeka na pierwszy import."),
            new MetricTileViewModel("Najnowszy przejazd", "--", "Pojawi sie po pierwszym pliku FIT albo GPX."),
            new MetricTileViewModel("Sredni dystans", "--", "Potrzebujemy przynajmniej jednej aktywnosci."),
            new MetricTileViewModel("Najdluzszy trening", "--", "Na razie baza jest celowo pusta."),
        ];
    }

    private void GoToPreviousPage()
    {
        if (!CanGoPreviousPage)
        {
            return;
        }

        CurrentPage--;
        RefreshRideLibraryPage();
    }

    private void GoToNextPage()
    {
        if (!CanGoNextPage)
        {
            return;
        }

        CurrentPage++;
        RefreshRideLibraryPage();
    }

    private void RefreshRideLibraryPage()
    {
        var skip = (CurrentPage - 1) * PageSize;

        RideLibrary =
        [
            .. _allRideLibrary
                .Skip(skip)
                .Take(PageSize),
        ];

        OnPropertyChanged(nameof(HasPagination));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        OnPropertyChanged(nameof(PaginationLabel));
        _previousPageCommand.NotifyCanExecuteChanged();
        _nextPageCommand.NotifyCanExecuteChanged();
    }
}
