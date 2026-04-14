using VeloCenter.Core.Integrations;

namespace VeloCenter.Infrastructure.Integrations.Strava;

public sealed class DisabledStravaIntegrationService : IStravaIntegrationService
{
    public StravaConnectionState GetConnectionState() =>
        new(
            false,
            false,
            null,
            null,
            null,
            []);

    public Task<StravaConnectionState> SaveManualConfigurationAsync(
        StravaManualConfiguration configuration,
        CancellationToken cancellationToken = default) =>
        Task.FromException<StravaConnectionState>(
            new InvalidOperationException("Konfiguracja Stravy nie jest dostepna w tym trybie aplikacji."));

    public Task<StravaConnectionState> ConnectAsync(CancellationToken cancellationToken = default) =>
        Task.FromException<StravaConnectionState>(
            new InvalidOperationException("Skonfiguruj lokalne dane swojej aplikacji Strava."));

    public Task<StravaSyncResult> SyncActivitiesAsync(
        IProgress<StravaSyncProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.FromException<StravaSyncResult>(
            new InvalidOperationException("Polaczenie ze Strava nie jest jeszcze skonfigurowane."));
}
