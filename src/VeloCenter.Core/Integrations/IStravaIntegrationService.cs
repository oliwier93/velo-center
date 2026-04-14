namespace VeloCenter.Core.Integrations;

public interface IStravaIntegrationService
{
    StravaConnectionState GetConnectionState();

    Task<StravaConnectionState> SaveManualConfigurationAsync(
        StravaManualConfiguration configuration,
        CancellationToken cancellationToken = default);

    Task<StravaConnectionState> DisconnectAsync(CancellationToken cancellationToken = default);

    Task<StravaConnectionState> ConnectAsync(CancellationToken cancellationToken = default);

    Task<StravaSyncResult> SyncActivitiesAsync(
        IProgress<StravaSyncProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
