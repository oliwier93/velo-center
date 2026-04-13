namespace VeloCenter.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
        : this(new VeloCenter.Infrastructure.Activities.InMemoryActivityRepository())
    {
    }

    public MainWindowViewModel(VeloCenter.Core.Activities.IActivityRepository activityRepository)
    {
        var activities = activityRepository.GetRecentActivities();

        RecentActivities =
        [
            .. System.Linq.Enumerable.Select(activities, activity => new RecentRideViewModel(activity)),
        ];
        Overview = VeloCenter.Core.Activities.TrainingOverview.FromActivities(activities);
        NextSteps =
        [
            "Replace the in-memory repository with SQLite and EF Core.",
            "Add the first importer for FIT or GPX files.",
            "Build activity details and weekly trend charts.",
        ];
    }

    public string AppTitle { get; } = "velo-center";

    public string Subtitle { get; } = "Desktop starter for local cycling analysis on Avalonia.";

    public string CurrentFocus { get; } = "MVP: import rides, store them locally, and surface weekly progress.";

    public VeloCenter.Core.Activities.TrainingOverview Overview { get; }

    public System.Collections.Generic.IReadOnlyList<RecentRideViewModel> RecentActivities { get; }

    public System.Collections.Generic.IReadOnlyList<string> NextSteps { get; }
}
