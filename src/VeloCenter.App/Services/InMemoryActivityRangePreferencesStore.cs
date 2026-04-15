using VeloCenter.App.Models;

namespace VeloCenter.App.Services;

public sealed class InMemoryActivityRangePreferencesStore : IActivityRangePreferencesStore
{
    private ActivityRangeSelection _selection = ActivityRangeSelection.Default;

    public ActivityRangeSelection Load() => _selection;

    public void Save(ActivityRangeSelection selection)
    {
        _selection = selection;
    }
}
