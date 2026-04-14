namespace VeloCenter.Core.Integrations;

public sealed record StravaConnectionState(
    bool IsConfigured,
    bool IsConnected,
    long? AthleteId,
    string? AthleteName,
    DateTimeOffset? LastSyncedAt,
    IReadOnlyList<string> GrantedScopes);
