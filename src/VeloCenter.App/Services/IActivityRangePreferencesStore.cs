using VeloCenter.App.Models;

namespace VeloCenter.App.Services;

public interface IActivityRangePreferencesStore
{
    ActivityRangeSelection Load();

    void Save(ActivityRangeSelection selection);
}
