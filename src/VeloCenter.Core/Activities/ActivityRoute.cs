namespace VeloCenter.Core.Activities;

public sealed record ActivityRoute(
    Guid ActivityId,
    ActivitySource Source,
    string Title,
    DateTimeOffset StartTime,
    IReadOnlyList<ActivityRoutePoint> Points);
