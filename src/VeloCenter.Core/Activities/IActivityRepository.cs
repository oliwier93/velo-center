namespace VeloCenter.Core.Activities;

public interface IActivityRepository
{
    IReadOnlyList<ActivitySummary> GetRecentActivities();

    IReadOnlyList<ActivityRoute> GetActivityRoutes(DateTime? startDate = null, DateTime? endDate = null);
}
