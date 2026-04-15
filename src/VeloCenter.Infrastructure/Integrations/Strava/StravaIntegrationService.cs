using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using VeloCenter.Core.Activities;
using VeloCenter.Core.Integrations;
using VeloCenter.Infrastructure.Persistence;

namespace VeloCenter.Infrastructure.Integrations.Strava;

public sealed class StravaIntegrationService(string databasePath) : IStravaIntegrationService
{
    private const string AuthorizationUrl = "https://www.strava.com/oauth/authorize";
    private const string TokenUrl = "https://www.strava.com/oauth/token";
    private const string ActivitiesUrl = "https://www.strava.com/api/v3/athlete/activities";
    private const string RequiredScopes = "activity:read_all,profile:read_all";
    private const int PageSize = 100;
    private const int MaxStoredRoutePoints = 300;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private static readonly HttpClient HttpClient = new();

    private readonly DbContextOptions<VeloCenterDbContext> _dbContextOptions = VeloCenterSqliteDatabase.CreateOptions(databasePath);
    private readonly string _configPath = GetConfigurationPath();
    private readonly string _sessionPath = GetSessionPath();

    public StravaConnectionState GetConnectionState()
    {
        var configuration = ReadConfiguration();
        var session = LoadSession();

        return new StravaConnectionState(
            configuration.IsConfigured,
            configuration.IsConfigured && session is not null,
            session?.AthleteId,
            session?.AthleteName,
            session?.LastSyncedAt,
            session?.GrantedScopes ?? []);
    }

    public async Task<StravaConnectionState> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var configuration = ReadConfiguration();

        if (!configuration.IsConfigured)
        {
            throw new InvalidOperationException("Najpierw wpisz lokalnie Client ID i Client Secret swojej aplikacji Strava.");
        }

        var port = GetFreeTcpPort();
        var redirectUri = $"http://127.0.0.1:{port}/strava/callback/";
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);

        try
        {
            listener.Start();
        }
        catch (HttpListenerException exception)
        {
            throw new InvalidOperationException("Nie udalo sie uruchomic lokalnego callbacku OAuth dla Stravy.", exception);
        }

        var authorizationUri = BuildAuthorizationUri(configuration.ClientId!, redirectUri, state);
        OpenBrowser(authorizationUri);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

        var context = await GetCallbackContextAsync(listener, timeoutCts.Token);
        var code = context.Request.QueryString["code"];
        var returnedState = context.Request.QueryString["state"];
        var error = context.Request.QueryString["error"];

        if (!string.IsNullOrWhiteSpace(error))
        {
            await WriteCallbackResponseAsync(context.Response, "Autoryzacja Strava zostala anulowana lub zakonczona bledem.");
            throw new InvalidOperationException($"Strava zwrocila blad autoryzacji: {error}.");
        }

        if (string.IsNullOrWhiteSpace(code) || !string.Equals(state, returnedState, StringComparison.Ordinal))
        {
            await WriteCallbackResponseAsync(context.Response, "Nie udalo sie potwierdzic odpowiedzi z autoryzacji Strava.");
            throw new InvalidOperationException("Odebrano niepoprawna odpowiedz z autoryzacji Stravy.");
        }

        var session = await ExchangeAuthorizationCodeAsync(configuration, code, cancellationToken);
        SaveSession(session);
        await WriteCallbackResponseAsync(context.Response, "Polaczenie ze Strava zakonczone. Mozesz wrocic do aplikacji Velo Center.");

        return new StravaConnectionState(
            true,
            true,
            session.AthleteId,
            session.AthleteName,
            session.LastSyncedAt,
            session.GrantedScopes);
    }

    public async Task<StravaConnectionState> SaveManualConfigurationAsync(
        StravaManualConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var normalizedClientId = configuration.ClientId.Trim();
        var normalizedClientSecret = configuration.ClientSecret.Trim();

        if (string.IsNullOrWhiteSpace(normalizedClientId) ||
            string.IsNullOrWhiteSpace(normalizedClientSecret))
        {
            throw new InvalidOperationException("Uzupelnij Client ID i Client Secret swojej aplikacji Strava.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var previousConfiguration = LoadConfiguration();
        var configurationChanged =
            !string.Equals(previousConfiguration?.ClientId, normalizedClientId, StringComparison.Ordinal) ||
            !string.Equals(previousConfiguration?.ClientSecret, normalizedClientSecret, StringComparison.Ordinal);

        SaveConfiguration(new StravaStoredConfiguration
        {
            ClientId = normalizedClientId,
            ClientSecret = normalizedClientSecret,
        });

        if (configurationChanged)
        {
            DeleteSession();
        }

        return GetConnectionState();
    }

    public Task<StravaConnectionState> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DeleteSession();
        DeleteConfiguration();

        return Task.FromResult(GetConnectionState());
    }

    public async Task<StravaSyncResult> SyncActivitiesAsync(
        IProgress<StravaSyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var configuration = ReadConfiguration();

        if (!configuration.IsConfigured)
        {
            throw new InvalidOperationException("Najpierw wpisz lokalnie Client ID i Client Secret swojej aplikacji Strava.");
        }

        var session = await EnsureActiveSessionAsync(configuration, cancellationToken);
        var pagesFetched = 0;
        var processedActivities = 0;
        var matchedActivities = 0;
        var skippedActivities = 0;
        var createdActivities = 0;
        var updatedActivities = 0;

        for (var page = 1; ; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{ActivitiesUrl}?page={page}&per_page={PageSize}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

            using var response = await HttpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == (HttpStatusCode)429)
            {
                var waitTime = GetWaitTimeFromHeaders(response.Headers);
                progress?.Report(new StravaSyncProgress(
                    page,
                    processedActivities,
                    matchedActivities,
                    skippedActivities,
                    createdActivities,
                    updatedActivities,
                    CalculateSyncProgressHint(page, false),
                    $"Limit Stravy osiagniety. Czekam {Math.Ceiling(waitTime.TotalSeconds)} s przed kolejnym oknem.",
                    true));
                await Task.Delay(waitTime, cancellationToken);
                page--;
                continue;
            }

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            var activities = JsonSerializer.Deserialize<List<StravaActivitySummaryResponse>>(payload, JsonOptions) ?? [];

            if (activities.Count == 0)
            {
                break;
            }

            processedActivities += activities.Count;
            var filteredActivities = activities
                .Where(activity => StravaActivityFilter.IsOutdoorCyclingActivity(activity.SportType, activity.Trainer, activity.LegacyType))
                .ToList();

            matchedActivities += filteredActivities.Count;
            skippedActivities += activities.Count - filteredActivities.Count;

            var batchResult = filteredActivities.Count == 0
                ? new BatchUpsertResult(0, 0)
                : UpsertStravaActivities(filteredActivities);
            pagesFetched++;
            createdActivities += batchResult.CreatedActivities;
            updatedActivities += batchResult.UpdatedActivities;

            progress?.Report(new StravaSyncProgress(
                page,
                processedActivities,
                matchedActivities,
                skippedActivities,
                createdActivities,
                updatedActivities,
                CalculateSyncProgressHint(page, activities.Count == PageSize),
                $"Strava: strona {page}, dopasowano {matchedActivities} aktywnosci rowerowych outdoor, pominieto {skippedActivities}.",
                false));

            if (TryGetThrottleDelay(response.Headers, out var throttleDelay))
            {
                progress?.Report(new StravaSyncProgress(
                    page,
                    processedActivities,
                    matchedActivities,
                    skippedActivities,
                    createdActivities,
                    updatedActivities,
                    CalculateSyncProgressHint(page, true),
                    $"Zblizam sie do limitu Stravy. Pauza {Math.Ceiling(throttleDelay.TotalSeconds)} s przed kolejnym pakietem.",
                    true));
                await Task.Delay(throttleDelay, cancellationToken);
            }
            else
            {
                await Task.Delay(TimeSpan.FromMilliseconds(180), cancellationToken);
            }

            if (activities.Count < PageSize)
            {
                break;
            }
        }

        session.LastSyncedAt = DateTimeOffset.UtcNow;
        SaveSession(session);

        return new StravaSyncResult(
            processedActivities,
            matchedActivities,
            skippedActivities,
            createdActivities,
            updatedActivities,
            pagesFetched,
            session.LastSyncedAt.Value);
    }

    private BatchUpsertResult UpsertStravaActivities(IReadOnlyList<StravaActivitySummaryResponse> activities)
    {
        using var dbContext = new VeloCenterDbContext(_dbContextOptions);
        var activityIds = activities
            .Select(activity => activity.Id.ToString())
            .ToHashSet(StringComparer.Ordinal);
        var importedAt = DateTimeOffset.UtcNow;

        var existingActivities = dbContext.Activities
            .Where(activity => activity.Source == ActivitySource.Strava &&
                               activity.SourceActivityId != null &&
                               activityIds.Contains(activity.SourceActivityId))
            .ToDictionary(activity => activity.SourceActivityId!, StringComparer.Ordinal);

        var createdActivities = 0;
        var updatedActivities = 0;

        foreach (var activity in activities)
        {
            var sourceActivityId = activity.Id.ToString();
            var isNewRecord = !existingActivities.TryGetValue(sourceActivityId, out var record);

            if (isNewRecord)
            {
                record = new ActivityRecord
                {
                    Id = Guid.NewGuid(),
                    Source = ActivitySource.Strava,
                    SourceActivityId = sourceActivityId,
                    ImportedAt = importedAt,
                };

                dbContext.Add(record);
                existingActivities[sourceActivityId] = record;
                createdActivities++;
            }
            else
            {
                updatedActivities++;
            }

            ArgumentNullException.ThrowIfNull(record);
            record.Title = string.IsNullOrWhiteSpace(activity.Name) ? $"Strava {sourceActivityId}" : activity.Name.Trim();
            record.StartTime = activity.StartDate;
            record.DistanceKm = Math.Round(activity.DistanceMeters / 1000d, 2);
            record.DurationSeconds = Math.Max(0, activity.ElapsedTimeSeconds);
            record.LastUpdatedAt = importedAt;
            ReplaceRoutePoints(dbContext, record.Id, DecodeSummaryPolyline(activity.Map?.SummaryPolyline));
        }

        dbContext.SaveChanges();

        return new BatchUpsertResult(createdActivities, updatedActivities);
    }

    private static void ReplaceRoutePoints(
        VeloCenterDbContext dbContext,
        Guid activityId,
        IReadOnlyList<ActivityRoutePoint> routePoints)
    {
        var existingRoutePoints = dbContext.ActivityRoutePoints
            .Where(point => point.ActivityId == activityId)
            .ToList();

        if (existingRoutePoints.Count > 0)
        {
            dbContext.ActivityRoutePoints.RemoveRange(existingRoutePoints);
        }

        if (routePoints.Count == 0)
        {
            return;
        }

        dbContext.ActivityRoutePoints.AddRange(
            routePoints.Select((point, index) => new ActivityRoutePointRecord
            {
                ActivityId = activityId,
                Sequence = index,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
            }));
    }

    private static IReadOnlyList<ActivityRoutePoint> DecodeSummaryPolyline(string? summaryPolyline)
    {
        if (string.IsNullOrWhiteSpace(summaryPolyline))
        {
            return [];
        }

        var points = new List<ActivityRoutePoint>();
        var latitude = 0;
        var longitude = 0;
        var index = 0;

        while (index < summaryPolyline.Length)
        {
            latitude += DecodePolylineValue(summaryPolyline, ref index);
            longitude += DecodePolylineValue(summaryPolyline, ref index);
            points.Add(new ActivityRoutePoint(latitude / 1E5, longitude / 1E5));
        }

        return SimplifyRoutePoints(points);
    }

    private static int DecodePolylineValue(string encoded, ref int index)
    {
        var result = 0;
        var shift = 0;
        int chunk;

        do
        {
            chunk = encoded[index++] - 63;
            result |= (chunk & 0x1F) << shift;
            shift += 5;
        }
        while (chunk >= 0x20 && index < encoded.Length);

        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }

    private static IReadOnlyList<ActivityRoutePoint> SimplifyRoutePoints(IReadOnlyList<ActivityRoutePoint> points)
    {
        if (points.Count <= MaxStoredRoutePoints)
        {
            return points;
        }

        var simplifiedPoints = new List<ActivityRoutePoint>(MaxStoredRoutePoints);
        var step = (double)(points.Count - 1) / (MaxStoredRoutePoints - 1);

        for (var index = 0; index < MaxStoredRoutePoints; index++)
        {
            var sourceIndex = (int)Math.Round(index * step);
            var clampedIndex = Math.Clamp(sourceIndex, 0, points.Count - 1);
            simplifiedPoints.Add(points[clampedIndex]);
        }

        return simplifiedPoints;
    }

    private async Task<StravaSession> EnsureActiveSessionAsync(
        StravaConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var session = LoadSession();

        if (session is null)
        {
            throw new InvalidOperationException("Najpierw polacz konto Strava.");
        }

        if (session.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return session;
        }

        var refreshedSession = await RefreshTokenAsync(configuration, session.RefreshToken, cancellationToken, session.LastSyncedAt);
        SaveSession(refreshedSession);

        return refreshedSession;
    }

    private static Uri BuildAuthorizationUri(string clientId, string redirectUri, string state)
    {
        var query = new[]
        {
            $"client_id={Uri.EscapeDataString(clientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            "response_type=code",
            "approval_prompt=auto",
            $"scope={Uri.EscapeDataString(RequiredScopes)}",
            $"state={Uri.EscapeDataString(state)}",
        };

        return new Uri($"{AuthorizationUrl}?{string.Join("&", query)}");
    }

    private static void OpenBrowser(Uri authorizationUri)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = authorizationUri.ToString(),
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Nie udalo sie otworzyc przegladarki dla logowania Strava.", exception);
        }
    }

    private static async Task<HttpListenerContext> GetCallbackContextAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        using var cancellationRegistration = cancellationToken.Register(() => listener.Stop());

        try
        {
            return await listener.GetContextAsync();
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Przekroczono czas oczekiwania na autoryzacje Strava.", exception);
        }
    }

    private async Task<StravaSession> ExchangeAuthorizationCodeAsync(
        StravaConfiguration configuration,
        string code,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", configuration.ClientId!),
            new KeyValuePair<string, string>("client_secret", configuration.ClientSecret!),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
        ]);

        using var response = await HttpClient.PostAsync(TokenUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<StravaTokenResponse>(payload, JsonOptions)
            ?? throw new InvalidOperationException("Strava zwrocila pusta odpowiedz tokenowa.");

        return ToSession(tokenResponse, null);
    }

    private async Task<StravaSession> RefreshTokenAsync(
        StravaConfiguration configuration,
        string refreshToken,
        CancellationToken cancellationToken,
        DateTimeOffset? lastSyncedAt)
    {
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", configuration.ClientId!),
            new KeyValuePair<string, string>("client_secret", configuration.ClientSecret!),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
        ]);

        using var response = await HttpClient.PostAsync(TokenUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<StravaTokenResponse>(payload, JsonOptions)
            ?? throw new InvalidOperationException("Strava zwrocila pusta odpowiedz tokenowa.");

        return ToSession(tokenResponse, lastSyncedAt);
    }

    private StravaConfiguration ReadConfiguration()
    {
        var storedConfiguration = LoadConfiguration();

        if (storedConfiguration?.IsConfigured is true)
        {
            return new StravaConfiguration(storedConfiguration.ClientId, storedConfiguration.ClientSecret);
        }

        return new StravaConfiguration(
            Environment.GetEnvironmentVariable("VELOCENTER_STRAVA_CLIENT_ID"),
            Environment.GetEnvironmentVariable("VELOCENTER_STRAVA_CLIENT_SECRET"));
    }

    private StravaStoredConfiguration? LoadConfiguration()
    {
        if (!File.Exists(_configPath))
        {
            return null;
        }

        var json = File.ReadAllText(_configPath);
        return JsonSerializer.Deserialize<StravaStoredConfiguration>(json, JsonOptions);
    }

    private void SaveConfiguration(StravaStoredConfiguration configuration)
    {
        var directory = Path.GetDirectoryName(_configPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(configuration, JsonOptions);
        File.WriteAllText(_configPath, json);
    }

    private void DeleteConfiguration()
    {
        if (File.Exists(_configPath))
        {
            File.Delete(_configPath);
        }
    }

    private StravaSession? LoadSession()
    {
        if (!File.Exists(_sessionPath))
        {
            return null;
        }

        var json = File.ReadAllText(_sessionPath);
        var session = JsonSerializer.Deserialize<StravaSession>(json, JsonOptions);

        return session;
    }

    private void SaveSession(StravaSession session)
    {
        var directory = Path.GetDirectoryName(_sessionPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(session, JsonOptions);
        File.WriteAllText(_sessionPath, json);
    }

    private void DeleteSession()
    {
        if (File.Exists(_sessionPath))
        {
            File.Delete(_sessionPath);
        }
    }

    private static async Task WriteCallbackResponseAsync(HttpListenerResponse response, string message)
    {
        response.ContentType = "text/html; charset=utf-8";

        var html =
            $$"""
            <!doctype html>
            <html lang="pl">
            <head>
              <meta charset="utf-8" />
              <title>Velo Center</title>
            </head>
            <body style="font-family:Segoe UI, sans-serif; padding:24px; background:#10151c; color:#f4f7fb;">
              <h1 style="font-size:20px; margin-bottom:12px;">Velo Center</h1>
              <p>{{WebUtility.HtmlEncode(message)}}</p>
              <p>To okno mozesz juz zamknac.</p>
            </body>
            </html>
            """;
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string GetSessionPath()
    {
        return Path.Combine(VeloCenterSqliteDatabase.GetApplicationDataDirectory(), "strava-session.json");
    }

    private static string GetConfigurationPath()
    {
        return Path.Combine(VeloCenterSqliteDatabase.GetApplicationDataDirectory(), "strava-config.json");
    }

    private static bool TryGetThrottleDelay(HttpResponseHeaders headers, out TimeSpan delay)
    {
        delay = TimeSpan.Zero;

        if (!TryParseRateLimit(headers, "X-ReadRateLimit-Limit", out var limits) ||
            !TryParseRateLimit(headers, "X-ReadRateLimit-Usage", out var usage))
        {
            return false;
        }

        if (limits.ShortWindow <= 0 || usage.ShortWindow < Math.Max(0, limits.ShortWindow - 3))
        {
            return false;
        }

        delay = GetWaitTimeFromHeaders(headers);
        return delay > TimeSpan.Zero;
    }

    private static TimeSpan GetWaitTimeFromHeaders(HttpResponseHeaders headers)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var nextQuarterMinute = ((nowUtc.Minute / 15) + 1) * 15;
        var nextWindow = new DateTimeOffset(
            nowUtc.Year,
            nowUtc.Month,
            nowUtc.Day,
            nowUtc.Hour,
            0,
            0,
            TimeSpan.Zero);

        nextWindow = nextQuarterMinute >= 60
            ? nextWindow.AddHours(1)
            : nextWindow.AddMinutes(nextQuarterMinute);

        var delay = nextWindow - nowUtc + TimeSpan.FromSeconds(5);
        return delay < TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : delay;
    }

    private static bool TryParseRateLimit(HttpResponseHeaders headers, string headerName, out RateLimitWindow rateLimit)
    {
        rateLimit = default;

        if (!headers.TryGetValues(headerName, out var values))
        {
            return false;
        }

        var raw = values.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2 ||
            !int.TryParse(parts[0], out var shortWindow) ||
            !int.TryParse(parts[1], out var longWindow))
        {
            return false;
        }

        rateLimit = new RateLimitWindow(shortWindow, longWindow);
        return true;
    }

    private static double CalculateSyncProgressHint(int currentPage, bool mayHaveMorePages)
    {
        if (!mayHaveMorePages)
        {
            return 100;
        }

        return Math.Min(92, 18 + (currentPage * 9));
    }

    private static StravaSession ToSession(StravaTokenResponse response, DateTimeOffset? lastSyncedAt)
    {
        var athleteName = string.Join(
            ' ',
            new[] { response.Athlete?.Firstname, response.Athlete?.Lastname }
                .Where(value => !string.IsNullOrWhiteSpace(value)))
            .Trim();

        return new StravaSession
        {
            AccessToken = response.AccessToken,
            RefreshToken = response.RefreshToken,
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(response.ExpiresAtUnixSeconds),
            AthleteId = response.Athlete?.Id,
            AthleteName = string.IsNullOrWhiteSpace(athleteName) ? "Strava athlete" : athleteName,
            GrantedScopes = response.Scope?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [],
            LastSyncedAt = lastSyncedAt,
        };
    }

    private sealed record StravaConfiguration(string? ClientId, string? ClientSecret)
    {
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ClientId) &&
            !string.IsNullOrWhiteSpace(ClientSecret);
    }

    private sealed class StravaSession
    {
        public string AccessToken { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public DateTimeOffset ExpiresAt { get; set; }

        public long? AthleteId { get; set; }

        public string? AthleteName { get; set; }

        public string[] GrantedScopes { get; set; } = [];

        public DateTimeOffset? LastSyncedAt { get; set; }
    }

    private sealed class StravaStoredConfiguration
    {
        public string? ClientId { get; set; }

        public string? ClientSecret { get; set; }

        [JsonIgnore]
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ClientId) &&
            !string.IsNullOrWhiteSpace(ClientSecret);
    }

    private sealed record BatchUpsertResult(int CreatedActivities, int UpdatedActivities);

    private readonly record struct RateLimitWindow(int ShortWindow, int LongWindow);

    private sealed class StravaTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_at")]
        public long ExpiresAtUnixSeconds { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("athlete")]
        public StravaAthleteResponse? Athlete { get; set; }
    }

    private sealed class StravaAthleteResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("firstname")]
        public string? Firstname { get; set; }

        [JsonPropertyName("lastname")]
        public string? Lastname { get; set; }
    }

    private sealed class StravaActivitySummaryResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("start_date")]
        public DateTimeOffset StartDate { get; set; }

        [JsonPropertyName("distance")]
        public double DistanceMeters { get; set; }

        [JsonPropertyName("elapsed_time")]
        public int ElapsedTimeSeconds { get; set; }

        [JsonPropertyName("map")]
        public StravaActivityMapResponse? Map { get; set; }

        [JsonPropertyName("sport_type")]
        public string? SportType { get; set; }

        [JsonPropertyName("type")]
        public string? LegacyType { get; set; }

        [JsonPropertyName("trainer")]
        public bool? Trainer { get; set; }
    }

    private sealed class StravaActivityMapResponse
    {
        [JsonPropertyName("summary_polyline")]
        public string? SummaryPolyline { get; set; }
    }
}
