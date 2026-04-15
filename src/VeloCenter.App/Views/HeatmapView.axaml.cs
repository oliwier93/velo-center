using System.Text.Encodings.Web;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VeloCenter.App.ViewModels;

namespace VeloCenter.App.Views;

public partial class HeatmapView : UserControl
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private static readonly Uri WebViewBaseUri = new("https://velo-center.local/");

    public HeatmapView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => RefreshMap();
        VectorMapView.NavigationCompleted += (_, e) =>
        {
            if (!e.IsSuccess)
            {
                ShowMapState("Mapa wektorowa nie wystartowala", "NativeWebView nie zaladowal dokumentu mapy poprawnie.");
            }
        };
        VectorMapView.WebMessageReceived += (_, e) => HandleWebMessage(e.Body);
    }

    private void ZoomInClicked(object? sender, RoutedEventArgs e)
    {
        _ = VectorMapView.InvokeScript("window.veloHeatmap?.zoomIn()");
    }

    private void ZoomOutClicked(object? sender, RoutedEventArgs e)
    {
        _ = VectorMapView.InvokeScript("window.veloHeatmap?.zoomOut()");
    }

    private void RefreshMap()
    {
        if (DataContext is not HeatmapViewModel viewModel || !viewModel.HasRoutes)
        {
            HideMapState();
            return;
        }

        var stadiaApiKey = Environment.GetEnvironmentVariable("VELOCENTER_STADIA_API_KEY");
        if (string.IsNullOrWhiteSpace(stadiaApiKey))
        {
            ShowMapState(
                "Brak klucza Stadia Maps",
                "Ustaw VELOCENTER_STADIA_API_KEY i uruchom aplikacje ponownie, aby zaladowac mape wektorowa.");
            return;
        }

        ShowMapState("Ladowanie mapy wektorowej", "Przygotowuje styl Stadia i rysuje trasy w MapLibre.");

        try
        {
            VectorMapView.NavigateToString(BuildMapDocument(viewModel, stadiaApiKey), WebViewBaseUri);
        }
        catch (Exception exception)
        {
            ShowMapState("Nie udalo sie zaladowac mapy", exception.Message);
        }
    }

    private void HandleWebMessage(string? messageBody)
    {
        if (string.IsNullOrWhiteSpace(messageBody))
        {
            return;
        }

        try
        {
            using var payload = JsonDocument.Parse(messageBody);
            var root = payload.RootElement;
            var type = root.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : null;

            switch (type)
            {
                case "ready":
                    HideMapState();
                    break;
                case "error":
                    ShowMapState(
                        "Mapa wektorowa zglosila blad",
                        root.TryGetProperty("message", out var messageElement)
                            ? messageElement.GetString() ?? "MapLibre nie podal szczegolow bledu."
                            : "MapLibre nie podal szczegolow bledu.");
                    break;
            }
        }
        catch
        {
            ShowMapState("Mapa wektorowa zglosila blad", "Nie udalo sie odczytac komunikatu zwrotnego z dokumentu mapy.");
        }
    }

    private void ShowMapState(string title, string detail)
    {
        MapStateTitle.Text = title;
        MapStateDetail.Text = detail;
        MapStateOverlay.IsVisible = true;
    }

    private void HideMapState()
    {
        MapStateOverlay.IsVisible = false;
        MapStateTitle.Text = string.Empty;
        MapStateDetail.Text = string.Empty;
    }

    private static string BuildMapDocument(HeatmapViewModel viewModel, string stadiaApiKey)
    {
        const string HostSurfaceColor = "#211234";
        var vectorRoutes = BuildVectorRoutes(viewModel.Routes);
        var weightedSegments = BuildWeightedSegments(viewModel.Routes);
        var routeFeatureCollection = new
        {
            type = "FeatureCollection",
            features = vectorRoutes.Select(route => new
            {
                type = "Feature",
                properties = new
                {
                    id = route.ActivityId,
                    title = route.Title,
                    source = route.SourceLabel,
                    pointCount = route.Coordinates.Count,
                },
                geometry = new
                {
                    type = "LineString",
                    coordinates = route.Coordinates
                        .Select(coordinate => new[] { coordinate.Longitude, coordinate.Latitude })
                        .ToArray(),
                },
            }).ToArray(),
        };
        var heatFeatureCollection = new
        {
            type = "FeatureCollection",
            features = weightedSegments.Select(segment => new
            {
                type = "Feature",
                properties = new
                {
                    weight = segment.Weight,
                },
                geometry = new
                {
                    type = "Point",
                    coordinates = new[]
                    {
                        (segment.StartLongitude + segment.EndLongitude) / 2d,
                        (segment.StartLatitude + segment.EndLatitude) / 2d,
                    },
                },
            }).ToArray(),
        };

        var bounds = GetBounds(viewModel.Routes);
        var routeFeatureCollectionJson = JsonSerializer.Serialize(routeFeatureCollection, JsonOptions);
        var heatFeatureCollectionJson = JsonSerializer.Serialize(heatFeatureCollection, JsonOptions);
        var maxWeight = Math.Max(1, weightedSegments.Count == 0 ? 1 : weightedSegments.Max(segment => segment.Weight));
        var boundsJson = JsonSerializer.Serialize(new[]
        {
            new[] { bounds.MinLongitude, bounds.MinLatitude },
            new[] { bounds.MaxLongitude, bounds.MaxLatitude },
        }, JsonOptions);
        var styleUrl = $"https://tiles.stadiamaps.com/styles/alidade_smooth_dark.json?api_key={Uri.EscapeDataString(stadiaApiKey)}";

        return $$"""
<!doctype html>
<html lang="pl">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <link rel="stylesheet" href="https://unpkg.com/maplibre-gl@5.12.0/dist/maplibre-gl.css" />
  <style>
    html, body {
      margin: 0;
      width: 100%;
      height: 100%;
      background: {{HostSurfaceColor}};
      overflow: hidden;
    }

    #shell {
      position: absolute;
      inset: 0;
      overflow: hidden;
      border-radius: 28px;
      background: {{HostSurfaceColor}};
      clip-path: inset(0 round 28px);
    }

    #map {
      position: absolute;
      inset: 0;
      width: 100%;
      height: 100%;
      background: {{HostSurfaceColor}};
      border-radius: 28px;
      clip-path: inset(0 round 28px);
    }

    .maplibregl-map,
    .maplibregl-canvas-container,
    .maplibregl-canvas {
      border-radius: 28px;
      background: {{HostSurfaceColor}} !important;
      clip-path: inset(0 round 28px);
    }

    .maplibregl-ctrl-bottom-left,
    .maplibregl-ctrl-bottom-right,
    .maplibregl-ctrl-top-left,
    .maplibregl-ctrl-top-right {
      display: none !important;
    }
  </style>
</head>
<body>
  <div id="shell">
    <div id="map"></div>
  </div>
  <script src="https://unpkg.com/maplibre-gl@5.12.0/dist/maplibre-gl.js"></script>
  <script>
    const routeGeoJson = {{routeFeatureCollectionJson}};
    const routeHeatGeoJson = {{heatFeatureCollectionJson}};
    const bounds = {{boundsJson}};
    const styleUrl = "{{styleUrl}}";
    const maxWeight = {{maxWeight}};
    const routeLineWidthExpression = ['interpolate', ['linear'], ['zoom'],
      6, 1.8,
      9, 2.6,
      12, 4.2,
      15, 6.2];
    const routeGlowWidthExpression = ['+', routeLineWidthExpression, 5.4];
    const heatWeightExpression = maxWeight <= 1
      ? 0.45
      : ['interpolate', ['linear'], ['coalesce', ['get', 'weight'], 1],
          1, 0.24,
          Math.max(2, Math.ceil(maxWeight * 0.45)), 0.62,
          maxWeight, 1];
    const firstSymbolLayerId = (style) => style?.layers?.find(layer => layer.type === 'symbol')?.id;

    const sendMessage = (payload) => {
      const serializedPayload = typeof payload === 'string' ? payload : JSON.stringify(payload);

      try {
        if (window.chrome?.webview?.postMessage) {
          window.chrome.webview.postMessage(serializedPayload);
          return true;
        }
      } catch { }

      try {
        if (window.parent && window.parent !== window) {
          window.parent.postMessage(serializedPayload, '*');
          return true;
        }
      } catch { }

      try {
        if (typeof invokeCSharpAction === 'function') {
          invokeCSharpAction(serializedPayload);
          return true;
        }
      } catch { }

      return false;
    };

    window.addEventListener('error', (event) => {
      sendMessage({
        type: 'error',
        message: event?.error?.message ?? event?.message ?? 'Nieznany blad JavaScript.'
      });
    });

    window.addEventListener('unhandledrejection', (event) => {
      sendMessage({
        type: 'error',
        message: event?.reason?.message ?? String(event?.reason ?? 'Nieznana obietnica zakonczona bledem.')
      });
    });

    try {
      const map = new maplibregl.Map({
        container: 'map',
        style: styleUrl,
        attributionControl: false,
        antialias: true,
        dragRotate: false,
        touchZoomRotate: false,
        pitchWithRotate: false
      });

      const resizeMap = () => window.requestAnimationFrame(() => map.resize());

      window.veloHeatmap = {
        zoomIn: () => map.zoomIn({ duration: 180 }),
        zoomOut: () => map.zoomOut({ duration: 180 })
      };

      window.addEventListener('resize', resizeMap);

      map.on('load', () => {
        const labelLayerId = firstSymbolLayerId(map.getStyle());

        map.addSource('route-heat-points', {
          type: 'geojson',
          data: routeHeatGeoJson
        });

        map.addSource('routes', {
          type: 'geojson',
          data: routeGeoJson
        });

        map.addLayer({
          id: 'route-heat',
          type: 'heatmap',
          source: 'route-heat-points',
          paint: {
            'heatmap-weight': heatWeightExpression,
            'heatmap-intensity': ['interpolate', ['linear'], ['zoom'],
              6, 0.85,
              10, 1.1,
              14, 1.35],
            'heatmap-radius': ['interpolate', ['linear'], ['zoom'],
              6, 10,
              10, 18,
              14, 30,
              16, 42],
            'heatmap-color': ['interpolate', ['linear'], ['heatmap-density'],
              0, 'rgba(0, 0, 0, 0)',
              0.12, 'rgba(255, 120, 182, 0.18)',
              0.35, 'rgba(255, 120, 182, 0.42)',
              0.62, 'rgba(156, 78, 208, 0.68)',
              0.82, 'rgba(92, 30, 133, 0.86)',
              1, 'rgba(49, 16, 65, 0.98)'],
            'heatmap-opacity': ['interpolate', ['linear'], ['zoom'],
              6, 0.96,
              12, 0.82,
              16, 0.62]
          }
        }, labelLayerId);

        map.addLayer({
          id: 'route-glow',
          type: 'line',
          source: 'routes',
          layout: {
            'line-cap': 'round',
            'line-join': 'round'
          },
          paint: {
            'line-color': '#ff8ec2',
            'line-width': routeGlowWidthExpression,
            'line-opacity': ['interpolate', ['linear'], ['zoom'],
              6, 0.10,
              12, 0.16,
              16, 0.24],
            'line-blur': 1.25
          }
        }, labelLayerId);

        map.addLayer({
          id: 'route-core',
          type: 'line',
          source: 'routes',
          layout: {
            'line-cap': 'round',
            'line-join': 'round'
          },
          paint: {
            'line-color': '#ffd6ea',
            'line-width': routeLineWidthExpression,
            'line-opacity': ['interpolate', ['linear'], ['zoom'],
              6, 0.18,
              12, 0.28,
              16, 0.42]
          }
        }, labelLayerId);

        if (bounds?.length === 2) {
          map.fitBounds(bounds, {
            padding: 40,
            duration: 0,
            maxZoom: 16
          });
        }

        resizeMap();
        map.once('idle', () => sendMessage({ type: 'ready' }));
      });

      map.on('error', (event) => {
        const message = event?.error?.message ?? 'MapLibre nie zglosil szczegolow.';
        sendMessage({ type: 'error', message });
      });
    } catch (error) {
      sendMessage({
        type: 'error',
        message: error?.message ?? 'Nie udalo sie uruchomic MapLibre GL JS.'
      });
    }
  </script>
</body>
</html>
""";
    }

    private static (double MinLatitude, double MaxLatitude, double MinLongitude, double MaxLongitude) GetBounds(
        IReadOnlyList<HeatmapRouteViewModel> routes)
    {
        var minLatitude = double.MaxValue;
        var maxLatitude = double.MinValue;
        var minLongitude = double.MaxValue;
        var maxLongitude = double.MinValue;

        foreach (var point in routes.SelectMany(route => route.Points))
        {
            minLatitude = Math.Min(minLatitude, point.Latitude);
            maxLatitude = Math.Max(maxLatitude, point.Latitude);
            minLongitude = Math.Min(minLongitude, point.Longitude);
            maxLongitude = Math.Max(maxLongitude, point.Longitude);
        }

        var latitudePadding = Math.Max(0.01, (maxLatitude - minLatitude) * 0.12);
        var longitudePadding = Math.Max(0.01, (maxLongitude - minLongitude) * 0.12);

        return (
            minLatitude - latitudePadding,
            maxLatitude + latitudePadding,
            minLongitude - longitudePadding,
            maxLongitude + longitudePadding);
    }

    private static IReadOnlyList<VectorRoute> BuildVectorRoutes(IReadOnlyList<HeatmapRouteViewModel> routes)
    {
        var vectorRoutes = new List<VectorRoute>(routes.Count);

        foreach (var route in routes)
        {
            var coordinates = CollapseConsecutiveRoutePoints(route.Points);
            if (coordinates.Count < 2)
            {
                continue;
            }

            vectorRoutes.Add(new VectorRoute(route.ActivityId, route.Title, route.SourceLabel, coordinates));
        }

        return vectorRoutes;
    }

    private static IReadOnlyList<WeightedRouteSegment> BuildWeightedSegments(IReadOnlyList<HeatmapRouteViewModel> routes)
    {
        var segments = new Dictionary<string, WeightedRouteSegment>();

        foreach (var route in routes)
        {
            if (route.Points.Count < 2)
            {
                continue;
            }

            var snappedPoints = CollapseConsecutiveDuplicates(route.Points);

            if (snappedPoints.Count < 2)
            {
                continue;
            }

            for (var index = 1; index < snappedPoints.Count; index++)
            {
                var start = snappedPoints[index - 1];
                var end = snappedPoints[index];

                if (start == end)
                {
                    continue;
                }

                var normalized = NormalizeSegment(start, end);
                var key = BuildSegmentKey(normalized.Start, normalized.End);

                if (segments.TryGetValue(key, out var existing))
                {
                    segments[key] = existing with { Weight = existing.Weight + 1 };
                    continue;
                }

                segments[key] = new WeightedRouteSegment(
                    normalized.Start.Latitude,
                    normalized.Start.Longitude,
                    normalized.End.Latitude,
                    normalized.End.Longitude,
                    1);
            }
        }

        return
        [
            .. segments.Values.OrderByDescending(segment => segment.Weight),
        ];
    }

    private static (SnappedPoint Start, SnappedPoint End) NormalizeSegment(SnappedPoint first, SnappedPoint second)
    {
        if (first.Latitude < second.Latitude)
        {
            return (first, second);
        }

        if (first.Latitude > second.Latitude)
        {
            return (second, first);
        }

        return first.Longitude <= second.Longitude
            ? (first, second)
            : (second, first);
    }

    private static string BuildSegmentKey(SnappedPoint start, SnappedPoint end) =>
        $"{start.Latitude:F4}:{start.Longitude:F4}:{end.Latitude:F4}:{end.Longitude:F4}";

    private static IReadOnlyList<RouteCoordinate> CollapseConsecutiveRoutePoints(IReadOnlyList<HeatmapPointViewModel> points)
    {
        var coordinates = new List<RouteCoordinate>(points.Count);

        foreach (var point in points)
        {
            var coordinate = new RouteCoordinate(point.Latitude, point.Longitude);
            if (coordinates.Count > 0 && AreSameCoordinate(coordinates[^1], coordinate))
            {
                continue;
            }

            coordinates.Add(coordinate);
        }

        return coordinates;
    }

    private static IReadOnlyList<SnappedPoint> CollapseConsecutiveDuplicates(IReadOnlyList<HeatmapPointViewModel> points)
    {
        var snappedPoints = new List<SnappedPoint>(points.Count);

        foreach (var point in points)
        {
            var snappedPoint = new SnappedPoint(SnapCoordinate(point.Latitude), SnapCoordinate(point.Longitude));

            if (snappedPoints.Count > 0 && snappedPoints[^1] == snappedPoint)
            {
                continue;
            }

            snappedPoints.Add(snappedPoint);
        }

        return snappedPoints;
    }

    private static double SnapCoordinate(double value) =>
        Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static bool AreSameCoordinate(RouteCoordinate left, RouteCoordinate right) =>
        Math.Abs(left.Latitude - right.Latitude) < 0.0000005d &&
        Math.Abs(left.Longitude - right.Longitude) < 0.0000005d;

    private readonly record struct RouteCoordinate(
        double Latitude,
        double Longitude);

    private readonly record struct SnappedPoint(
        double Latitude,
        double Longitude);

    private readonly record struct VectorRoute(
        Guid ActivityId,
        string Title,
        string SourceLabel,
        IReadOnlyList<RouteCoordinate> Coordinates);

    private readonly record struct WeightedRouteSegment(
        double StartLatitude,
        double StartLongitude,
        double EndLatitude,
        double EndLongitude,
        int Weight);
}
