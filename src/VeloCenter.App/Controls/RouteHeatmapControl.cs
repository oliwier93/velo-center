using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using VeloCenter.App.ViewModels;

namespace VeloCenter.App.Controls;

public sealed class RouteHeatmapControl : Control
{
    private const int TileSize = 256;
    private const int TileDensityScale = 2;
    private const int MinZoomLevel = 3;
    private const int MaxZoomLevel = 20;
    private static readonly TimeSpan TileCacheFreshness = TimeSpan.FromDays(30);
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly string StadiaTileCacheDirectory = Path.Combine(Path.GetTempPath(), "velo-center-map-cache", "stadiamaps-alidade-smooth-dark-2x");
    private static readonly Pen FramePen = new(new SolidColorBrush(Color.Parse("#2A6F7F9A")), 1);
    private static readonly Pen GlowPen = new(new SolidColorBrush(Color.Parse("#36FF95D9")), 11, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    private static readonly Pen SecondaryGlowPen = new(new SolidColorBrush(Color.Parse("#56E877C5")), 6.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    private static readonly Pen CorePen = new(new SolidColorBrush(Color.Parse("#96C14D97")), 2.6, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    private readonly Dictionary<string, Bitmap> _tileBitmaps = [];
    private TileViewport? _viewport;
    private int _tileRefreshVersion;
    private int _zoomBias;
    private string? _tileStatusMessage;
    private bool _isDragging;
    private Point? _lastPointerPosition;
    private double _panLatitudeOffset;
    private double _panLongitudeOffset;

    public static readonly StyledProperty<IReadOnlyList<HeatmapRouteViewModel>> RoutesProperty =
        AvaloniaProperty.Register<RouteHeatmapControl, IReadOnlyList<HeatmapRouteViewModel>>(nameof(Routes), []);

    public IReadOnlyList<HeatmapRouteViewModel> Routes
    {
        get => GetValue(RoutesProperty);
        set => SetValue(RoutesProperty, value);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Routes.Count == 0 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isDragging = true;
        _lastPointerPosition = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isDragging || _viewport is null || _lastPointerPosition is null)
        {
            return;
        }

        var currentPosition = e.GetPosition(this);
        var delta = currentPosition - _lastPointerPosition.Value;
        _lastPointerPosition = currentPosition;

        var viewport = _viewport.Value;
        if (viewport.PlotArea.Width <= 0 || viewport.PlotArea.Height <= 0)
        {
            return;
        }

        _panLongitudeOffset -= (delta.X / viewport.PlotArea.Width) * viewport.VisibleLongitudeSpan;
        _panLatitudeOffset += (delta.Y / viewport.PlotArea.Height) * viewport.VisibleLatitudeSpan;
        ResetViewport();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndDragging(e);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isDragging = false;
        _lastPointerPosition = null;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (e.Delta.Y > 0)
        {
            ZoomIn();
            e.Handled = true;
            return;
        }

        if (e.Delta.Y < 0)
        {
            ZoomOut();
            e.Handled = true;
        }
    }

    public void ZoomIn()
    {
        if (_zoomBias >= 5)
        {
            return;
        }

        _zoomBias++;
        ResetViewport();
    }

    public void ZoomOut()
    {
        if (_zoomBias <= -5)
        {
            return;
        }

        _zoomBias--;
        ResetViewport();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        context.DrawRectangle(new SolidColorBrush(Color.Parse("#11141A")), null, bounds);

        var plotArea = bounds.Deflate(new Thickness(24));
        if (plotArea.Width <= 0 || plotArea.Height <= 0)
        {
            return;
        }

        if (_viewport is null)
        {
            _viewport = BuildTileViewport(plotArea, Routes);
            _ = RefreshTilesAsync();
        }

        DrawTiles(context);
        DrawRoutes(context);
        DrawTileStatus(context, plotArea);
        context.DrawRectangle(null, FramePen, plotArea);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RoutesProperty)
        {
            _panLatitudeOffset = 0;
            _panLongitudeOffset = 0;
            ResetViewport();
            return;
        }

        if (change.Property == BoundsProperty)
        {
            ResetViewport();
        }
    }

    private void ResetViewport()
    {
        _viewport = null;
        _ = RefreshTilesAsync();
        InvalidateVisual();
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            UseProxy = false,
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(12),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VeloCenter/0.1.0 (+local desktop app)");
        return client;
    }

    private async Task RefreshTilesAsync()
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(GetStadiaApiKey()))
        {
            _tileStatusMessage = "Brak klucza Stadia Maps.\nUstaw VELOCENTER_STADIA_API_KEY i uruchom aplikacje ponownie.";
            await Dispatcher.UIThread.InvokeAsync(InvalidateVisual);
            return;
        }

        var viewport = BuildTileViewport(bounds.Deflate(new Thickness(24)), Routes);
        _viewport = viewport;
        var version = ++_tileRefreshVersion;
        var tileKeys = EnumerateTiles(viewport).ToList();
        var loadedTileCount = 0;
        var failedTileCount = 0;

        foreach (var tileKey in tileKeys)
        {
            if (_tileBitmaps.ContainsKey(tileKey.CacheKey))
            {
                loadedTileCount++;
                continue;
            }

            var bitmap = await LoadTileBitmapAsync(tileKey);
            if (version != _tileRefreshVersion)
            {
                return;
            }

            if (bitmap is null)
            {
                failedTileCount++;
                continue;
            }

            _tileBitmaps[tileKey.CacheKey] = bitmap;
            loadedTileCount++;
            await Dispatcher.UIThread.InvokeAsync(InvalidateVisual);
        }

        if (version != _tileRefreshVersion)
        {
            return;
        }

        _tileStatusMessage = loadedTileCount > 0
            ? null
            : failedTileCount > 0
                ? "Nie udalo sie pobrac kafelkow Stadia Maps.\nSprawdz klucz API i polaczenie z internetem."
                : "Heatmapa czeka na pierwsze kafelki Stadia Maps.";

        await Dispatcher.UIThread.InvokeAsync(InvalidateVisual);
    }

    private void DrawTiles(DrawingContext context)
    {
        if (_viewport is null)
        {
            return;
        }

        var viewport = _viewport.Value;

        foreach (var tile in EnumerateTiles(viewport))
        {
            if (!_tileBitmaps.TryGetValue(tile.CacheKey, out var bitmap))
            {
                continue;
            }

            context.DrawImage(bitmap, tile.DestinationRect);
        }
    }

    private void DrawRoutes(DrawingContext context)
    {
        if (_viewport is null || Routes.Count == 0)
        {
            return;
        }

        var viewport = _viewport.Value;

        foreach (var route in Routes)
        {
            if (route.Points.Count < 2)
            {
                continue;
            }

            var geometry = new StreamGeometry();

            using (var geometryContext = geometry.Open())
            {
                var firstPoint = Project(route.Points[0], viewport);
                geometryContext.BeginFigure(firstPoint, false);

                for (var index = 1; index < route.Points.Count; index++)
                {
                    geometryContext.LineTo(Project(route.Points[index], viewport));
                }
            }

            context.DrawGeometry(null, GlowPen, geometry);
            context.DrawGeometry(null, SecondaryGlowPen, geometry);
            context.DrawGeometry(null, CorePen, geometry);
        }
    }

    private void DrawTileStatus(DrawingContext context, Rect plotArea)
    {
        if (string.IsNullOrWhiteSpace(_tileStatusMessage) || Routes.Count == 0)
        {
            return;
        }

        var panelBackgroundBrush = ResolveBrush("PanelStrongBackgroundBrush", "#451B0F2A");
        var panelStrokeBrush = ResolveBrush("PanelStrokeBrush", "#3B4658");
        var titleBrush = ResolveBrush("TextPrimaryBrush", "#F7E9FF");
        var detailBrush = ResolveBrush("TextMutedBrush", "#C8B8D8");

        var overlayWidth = Math.Min(520, Math.Max(300, plotArea.Width - 48));
        var titleText = CreateText("Mapa nie zaladowala kafelkow", 18, titleBrush, FontWeight.Bold);
        var detailText = CreateText(_tileStatusMessage, 13, detailBrush, FontWeight.Medium);
        var overlayHeight = 28 + titleText.Height + 10 + detailText.Height + 28;
        var overlayRect = new Rect(
            plotArea.Left + ((plotArea.Width - overlayWidth) / 2),
            plotArea.Top + ((plotArea.Height - overlayHeight) / 2),
            overlayWidth,
            overlayHeight);

        context.DrawRectangle(panelBackgroundBrush, new Pen(panelStrokeBrush, 1), overlayRect, 22, 22);
        context.DrawText(titleText, new Point(overlayRect.Left + 22, overlayRect.Top + 20));
        context.DrawText(detailText, new Point(overlayRect.Left + 22, overlayRect.Top + 20 + titleText.Height + 10));
    }

    private TileViewport BuildTileViewport(Rect plotArea, IReadOnlyList<HeatmapRouteViewModel> routes)
    {
        var zoomedBounds = ApplyZoomBias(GetBounds(routes), _zoomBias);
        var (minLatitude, maxLatitude, minLongitude, maxLongitude) = ApplyPanOffset(zoomedBounds, _panLatitudeOffset, _panLongitudeOffset);
        var zoom = SelectZoom(plotArea, minLatitude, maxLatitude, minLongitude, maxLongitude);
        zoom = Math.Clamp(zoom, MinZoomLevel, MaxZoomLevel);

        var worldLeft = LongitudeToTileX(minLongitude, zoom) * TileSize;
        var worldRight = LongitudeToTileX(maxLongitude, zoom) * TileSize;
        var worldTop = LatitudeToTileY(maxLatitude, zoom) * TileSize;
        var worldBottom = LatitudeToTileY(minLatitude, zoom) * TileSize;

        var worldWidth = Math.Max(1, worldRight - worldLeft);
        var worldHeight = Math.Max(1, worldBottom - worldTop);
        var scale = Math.Max(plotArea.Width / worldWidth, plotArea.Height / worldHeight);
        var offsetX = plotArea.Left + ((plotArea.Width - (worldWidth * scale)) / 2);
        var offsetY = plotArea.Top + ((plotArea.Height - (worldHeight * scale)) / 2);

        var minTileX = (int)Math.Floor(worldLeft / TileSize) - 1;
        var maxTileX = (int)Math.Floor((worldRight - 1) / TileSize) + 1;
        var minTileY = (int)Math.Floor(worldTop / TileSize) - 1;
        var maxTileY = (int)Math.Floor((worldBottom - 1) / TileSize) + 1;

        return new TileViewport(
            plotArea,
            zoom,
            worldLeft,
            worldTop,
            scale,
            offsetX,
            offsetY,
            minTileX,
            maxTileX,
            minTileY,
            maxTileY,
            maxLatitude - minLatitude,
            maxLongitude - minLongitude);
    }

    private static (double MinLatitude, double MaxLatitude, double MinLongitude, double MaxLongitude) GetBounds(IReadOnlyList<HeatmapRouteViewModel> routes)
    {
        if (routes.Count == 0 || routes.All(route => route.Points.Count == 0))
        {
            const double fallbackLatitude = 52.2297;
            const double fallbackLongitude = 21.0122;
            return (fallbackLatitude - 0.08, fallbackLatitude + 0.08, fallbackLongitude - 0.11, fallbackLongitude + 0.11);
        }

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

    private static (double MinLatitude, double MaxLatitude, double MinLongitude, double MaxLongitude) ApplyZoomBias(
        (double MinLatitude, double MaxLatitude, double MinLongitude, double MaxLongitude) bounds,
        int zoomBias)
    {
        if (zoomBias == 0)
        {
            return bounds;
        }

        const double minLatitudeLimit = -85.05112878;
        const double maxLatitudeLimit = 85.05112878;
        const double minLongitudeLimit = -180d;
        const double maxLongitudeLimit = 180d;
        const double minLatitudeSpan = 0.002d;
        const double minLongitudeSpan = 0.002d;
        const double zoomStepFactor = 1.6d;

        var centerLatitude = (bounds.MinLatitude + bounds.MaxLatitude) / 2d;
        var centerLongitude = (bounds.MinLongitude + bounds.MaxLongitude) / 2d;
        var latitudeSpan = Math.Max(minLatitudeSpan, bounds.MaxLatitude - bounds.MinLatitude);
        var longitudeSpan = Math.Max(minLongitudeSpan, bounds.MaxLongitude - bounds.MinLongitude);
        var zoomFactor = Math.Pow(zoomStepFactor, zoomBias);

        var adjustedLatitudeSpan = Math.Clamp(
            latitudeSpan / zoomFactor,
            minLatitudeSpan,
            (maxLatitudeLimit - minLatitudeLimit) - 0.001d);
        var adjustedLongitudeSpan = Math.Clamp(
            longitudeSpan / zoomFactor,
            minLongitudeSpan,
            (maxLongitudeLimit - minLongitudeLimit) - 0.001d);

        var (minLatitude, maxLatitude) = ClampRange(centerLatitude, adjustedLatitudeSpan, minLatitudeLimit, maxLatitudeLimit);
        var (minLongitude, maxLongitude) = ClampRange(centerLongitude, adjustedLongitudeSpan, minLongitudeLimit, maxLongitudeLimit);

        return (minLatitude, maxLatitude, minLongitude, maxLongitude);
    }

    private static (double MinLatitude, double MaxLatitude, double MinLongitude, double MaxLongitude) ApplyPanOffset(
        (double MinLatitude, double MaxLatitude, double MinLongitude, double MaxLongitude) bounds,
        double latitudeOffset,
        double longitudeOffset)
    {
        const double minLatitudeLimit = -85.05112878;
        const double maxLatitudeLimit = 85.05112878;
        const double minLongitudeLimit = -180d;
        const double maxLongitudeLimit = 180d;

        var latitudeSpan = bounds.MaxLatitude - bounds.MinLatitude;
        var longitudeSpan = bounds.MaxLongitude - bounds.MinLongitude;
        var centerLatitude = ((bounds.MinLatitude + bounds.MaxLatitude) / 2d) + latitudeOffset;
        var centerLongitude = ((bounds.MinLongitude + bounds.MaxLongitude) / 2d) + longitudeOffset;

        var (minLatitude, maxLatitude) = ClampRange(centerLatitude, latitudeSpan, minLatitudeLimit, maxLatitudeLimit);
        var (minLongitude, maxLongitude) = ClampRange(centerLongitude, longitudeSpan, minLongitudeLimit, maxLongitudeLimit);

        return (minLatitude, maxLatitude, minLongitude, maxLongitude);
    }

    private static (double Min, double Max) ClampRange(
        double center,
        double span,
        double minLimit,
        double maxLimit)
    {
        var clampedSpan = Math.Min(span, maxLimit - minLimit);
        var min = center - (clampedSpan / 2d);
        var max = center + (clampedSpan / 2d);

        if (min < minLimit)
        {
            max += minLimit - min;
            min = minLimit;
        }

        if (max > maxLimit)
        {
            min -= max - maxLimit;
            max = maxLimit;
        }

        return (Math.Max(minLimit, min), Math.Min(maxLimit, max));
    }

    private static int SelectZoom(Rect plotArea, double minLatitude, double maxLatitude, double minLongitude, double maxLongitude)
    {
        for (var zoom = MaxZoomLevel; zoom >= MinZoomLevel; zoom--)
        {
            var width = Math.Abs(LongitudeToTileX(maxLongitude, zoom) - LongitudeToTileX(minLongitude, zoom)) * TileSize;
            var height = Math.Abs(LatitudeToTileY(minLatitude, zoom) - LatitudeToTileY(maxLatitude, zoom)) * TileSize;

            if (width <= plotArea.Width * 1.1 && height <= plotArea.Height * 1.1)
            {
                return zoom;
            }
        }

        return MinZoomLevel;
    }

    private static IEnumerable<TileInfo> EnumerateTiles(TileViewport viewport)
    {
        var maxTileIndex = (1 << viewport.Zoom) - 1;

        for (var tileX = viewport.MinTileX; tileX <= viewport.MaxTileX; tileX++)
        {
            for (var tileY = viewport.MinTileY; tileY <= viewport.MaxTileY; tileY++)
            {
                if (tileX < 0 || tileY < 0 || tileX > maxTileIndex || tileY > maxTileIndex)
                {
                    continue;
                }

                var destinationRect = new Rect(
                    viewport.OffsetX + (((tileX * TileSize) - viewport.WorldLeft) * viewport.Scale),
                    viewport.OffsetY + (((tileY * TileSize) - viewport.WorldTop) * viewport.Scale),
                    TileSize * viewport.Scale,
                    TileSize * viewport.Scale);

                yield return new TileInfo(viewport.Zoom, tileX, tileY, destinationRect);
            }
        }
    }

    private static async Task<Bitmap?> LoadTileBitmapAsync(TileInfo tileInfo)
    {
        var stadiaTilePath = EnsureTileCachePath(StadiaTileCacheDirectory, tileInfo);
        var cachedTile = TryLoadBitmapFromCache(stadiaTilePath);

        try
        {
            if (cachedTile is { IsFresh: true } freshTile)
            {
                return freshTile.Bitmap;
            }

            if (await TryDownloadTileBitmapAsync(BuildTileUri(tileInfo), stadiaTilePath) is { } stadiaDownloadedBitmap)
            {
                return stadiaDownloadedBitmap;
            }

            if (cachedTile is { } staleTile)
            {
                return staleTile.Bitmap;
            }
        }
        catch
        {
            // The stale cache fallback below already covers the expected failure modes.
        }

        return null;
    }

    private static Point Project(HeatmapPointViewModel point, TileViewport viewport)
    {
        var worldX = LongitudeToTileX(point.Longitude, viewport.Zoom) * TileSize;
        var worldY = LatitudeToTileY(point.Latitude, viewport.Zoom) * TileSize;

        return new Point(
            viewport.OffsetX + ((worldX - viewport.WorldLeft) * viewport.Scale),
            viewport.OffsetY + ((worldY - viewport.WorldTop) * viewport.Scale));
    }

    private static double LongitudeToTileX(double longitude, int zoom)
    {
        var tileCount = 1 << zoom;
        return ((longitude + 180d) / 360d) * tileCount;
    }

    private static double LatitudeToTileY(double latitude, int zoom)
    {
        var tileCount = 1 << zoom;
        var clampedLatitude = Math.Clamp(latitude, -85.05112878, 85.05112878);
        var latitudeRadians = clampedLatitude * Math.PI / 180d;
        return (1d - Math.Log(Math.Tan(latitudeRadians) + (1d / Math.Cos(latitudeRadians))) / Math.PI) / 2d * tileCount;
    }

    private static Uri BuildTileUri(TileInfo tileInfo)
    {
        var baseUri = $"https://tiles.stadiamaps.com/tiles/alidade_smooth_dark/{tileInfo.Zoom}/{tileInfo.X}/{tileInfo.Y}@{TileDensityScale}x.png";
        var stadiaApiKey = GetStadiaApiKey();

        if (string.IsNullOrWhiteSpace(stadiaApiKey))
        {
            return new Uri(baseUri);
        }

        return new Uri($"{baseUri}?api_key={Uri.EscapeDataString(stadiaApiKey)}");
    }

    private static string? GetStadiaApiKey() =>
        Environment.GetEnvironmentVariable("VELOCENTER_STADIA_API_KEY");

    private static string EnsureTileCachePath(string cacheRootDirectory, TileInfo tileInfo)
    {
        Directory.CreateDirectory(cacheRootDirectory);
        var tileDirectory = Path.Combine(cacheRootDirectory, tileInfo.Zoom.ToString(), tileInfo.X.ToString());
        Directory.CreateDirectory(tileDirectory);
        return Path.Combine(tileDirectory, $"{tileInfo.Y}@{TileDensityScale}x.png");
    }

    private static CachedTileBitmap? TryLoadBitmapFromCache(string tilePath)
    {
        try
        {
            if (!File.Exists(tilePath))
            {
                return null;
            }

            var isFresh = File.GetLastWriteTimeUtc(tilePath) > DateTime.UtcNow.Subtract(TileCacheFreshness);
            using var cachedStream = File.OpenRead(tilePath);
            return new CachedTileBitmap(new Bitmap(cachedStream), isFresh);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<Bitmap?> TryDownloadTileBitmapAsync(Uri tileUri, string tilePath)
    {
        try
        {
            await using var responseStream = await HttpClient.GetStreamAsync(tileUri);
            await using var memoryStream = new MemoryStream();
            await responseStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            await File.WriteAllBytesAsync(tilePath, memoryStream.ToArray());
            memoryStream.Position = 0;
            return new Bitmap(memoryStream);
        }
        catch
        {
            return null;
        }
    }

    private FormattedText CreateText(string text, double fontSize, IBrush foreground, FontWeight fontWeight)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, fontWeight),
            fontSize,
            foreground);
    }

    private IBrush ResolveBrush(string resourceKey, string fallbackHex)
    {
        if (TryGetResource(resourceKey, ActualThemeVariant, out var value) && value is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private void EndDragging(PointerReleasedEventArgs? releasedEventArgs)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        _lastPointerPosition = null;

        if (releasedEventArgs is not null && releasedEventArgs.Pointer.Captured == this)
        {
            releasedEventArgs.Pointer.Capture(null);
        }
    }

    private readonly record struct TileViewport(
        Rect PlotArea,
        int Zoom,
        double WorldLeft,
        double WorldTop,
        double Scale,
        double OffsetX,
        double OffsetY,
        int MinTileX,
        int MaxTileX,
        int MinTileY,
        int MaxTileY,
        double VisibleLatitudeSpan,
        double VisibleLongitudeSpan);

    private readonly record struct CachedTileBitmap(
        Bitmap Bitmap,
        bool IsFresh);

    private readonly record struct TileInfo(
        int Zoom,
        int X,
        int Y,
        Rect DestinationRect)
    {
        public string CacheKey => $"{Zoom}/{X}/{Y}";
    }
}
