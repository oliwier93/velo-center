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
    private static readonly string WebViewDocumentDirectory = Path.Combine(Path.GetTempPath(), "velo-center", "webview");
    private static readonly string WebViewDocumentPath = Path.Combine(WebViewDocumentDirectory, "heatmap-vector.html");

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
            Directory.CreateDirectory(WebViewDocumentDirectory);
            File.WriteAllText(WebViewDocumentPath, BuildMapDocument(viewModel, stadiaApiKey));
            VectorMapView.Source = new Uri(WebViewDocumentPath);
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
        var featureCollection = new
        {
            type = "FeatureCollection",
            features = viewModel.Routes.Select(route => new
            {
                type = "Feature",
                properties = new
                {
                    id = route.ActivityId,
                    title = route.Title,
                    source = route.SourceLabel,
                },
                geometry = new
                {
                    type = "LineString",
                    coordinates = route.Points.Select(point => new[] { point.Longitude, point.Latitude }).ToArray(),
                },
            }).ToArray(),
        };

        var bounds = GetBounds(viewModel.Routes);
        var featureCollectionJson = JsonSerializer.Serialize(featureCollection, JsonOptions);
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
      background: #0f1218;
      overflow: hidden;
    }

    #map {
      width: 100%;
      height: 100%;
      background: #0f1218;
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
  <div id="map"></div>
  <script src="https://unpkg.com/maplibre-gl@5.12.0/dist/maplibre-gl.js"></script>
  <script>
    const routeGeoJson = {{featureCollectionJson}};
    const bounds = {{boundsJson}};
    const styleUrl = "{{styleUrl}}";

    const sendMessage = (payload) => {
      if (typeof invokeCSharpAction === 'function') {
        invokeCSharpAction(JSON.stringify(payload));
      }
    };

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

      window.veloHeatmap = {
        zoomIn: () => map.zoomIn({ duration: 180 }),
        zoomOut: () => map.zoomOut({ duration: 180 })
      };

      map.on('load', () => {
        map.addSource('routes', {
          type: 'geojson',
          data: routeGeoJson
        });

        map.addLayer({
          id: 'route-glow',
          type: 'line',
          source: 'routes',
          layout: {
            'line-cap': 'round',
            'line-join': 'round'
          },
          paint: {
            'line-color': '#ff6fae',
            'line-width': 8,
            'line-opacity': 0.09,
            'line-blur': 0.9
          }
        });

        map.addLayer({
          id: 'route-core',
          type: 'line',
          source: 'routes',
          layout: {
            'line-cap': 'round',
            'line-join': 'round'
          },
          paint: {
            'line-color': '#ff6fae',
            'line-width': 3.5,
            'line-opacity': 0.24
          }
        });

        if (bounds?.length === 2) {
          map.fitBounds(bounds, {
            padding: 40,
            duration: 0,
            maxZoom: 16
          });
        }

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
}
