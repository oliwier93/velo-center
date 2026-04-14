namespace VeloCenter.Core.Integrations;

public sealed record StravaSyncProgress(
    int CurrentPage,
    int ProcessedActivities,
    int MatchedActivities,
    int SkippedActivities,
    int CreatedActivities,
    int UpdatedActivities,
    double ProgressHint,
    string Message,
    bool IsWaitingForRateLimitReset);
