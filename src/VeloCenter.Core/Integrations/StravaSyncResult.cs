namespace VeloCenter.Core.Integrations;

public sealed record StravaSyncResult(
    int ProcessedActivities,
    int MatchedActivities,
    int SkippedActivities,
    int CreatedActivities,
    int UpdatedActivities,
    int PagesFetched,
    DateTimeOffset CompletedAt);
