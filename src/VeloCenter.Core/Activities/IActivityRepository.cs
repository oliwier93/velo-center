namespace VeloCenter.Core.Activities;

public interface IActivityRepository
{
    IReadOnlyList<ActivitySummary> GetRecentActivities();
}
