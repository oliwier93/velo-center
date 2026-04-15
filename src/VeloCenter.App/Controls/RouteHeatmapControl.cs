using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VeloCenter.App.ViewModels;

namespace VeloCenter.App.Controls;

public sealed class RouteHeatmapControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<HeatmapRouteViewModel>> RoutesProperty =
        AvaloniaProperty.Register<RouteHeatmapControl, IReadOnlyList<HeatmapRouteViewModel>>(nameof(Routes), []);

    private static readonly Color BackgroundStartColor = Color.Parse("#101A2E");
    private static readonly Color BackgroundEndColor = Color.Parse("#08111E");
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.Parse("#1C8CA5C7")), 1, dashStyle: new DashStyle([4, 8], 0));
    private static readonly Pen FramePen = new(new SolidColorBrush(Color.Parse("#287596C9")), 1);
    private static readonly Pen GlowPen = new(new SolidColorBrush(Color.Parse("#1657E5FF")), 14, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    private static readonly Pen SecondaryGlowPen = new(new SolidColorBrush(Color.Parse("#209DF3FF")), 8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    private static readonly Pen CorePen = new(new SolidColorBrush(Color.Parse("#B4ECFF")), 2.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    private static readonly IBrush HighlightBrush = new SolidColorBrush(Color.Parse("#26D6F4FF"));

    public IReadOnlyList<HeatmapRouteViewModel> Routes
    {
        get => GetValue(RoutesProperty);
        set => SetValue(RoutesProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        context.DrawRectangle(
            new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                [
                    new GradientStop(BackgroundStartColor, 0),
                    new GradientStop(BackgroundEndColor, 1),
                ],
            },
            null,
            bounds);

        var plotArea = bounds.Deflate(new Thickness(24, 24, 24, 24));
        if (plotArea.Width <= 0 || plotArea.Height <= 0)
        {
            return;
        }

        DrawGrid(context, plotArea);
        DrawAmbientHighlights(context, plotArea);
        DrawRoutes(context, plotArea);
        context.DrawRectangle(null, FramePen, plotArea);
    }

    private static void DrawGrid(DrawingContext context, Rect plotArea)
    {
        const int verticalDivisions = 6;
        const int horizontalDivisions = 6;

        for (var index = 1; index < verticalDivisions; index++)
        {
            var x = plotArea.Left + (plotArea.Width / verticalDivisions * index);
            context.DrawLine(GridPen, new Point(x, plotArea.Top), new Point(x, plotArea.Bottom));
        }

        for (var index = 1; index < horizontalDivisions; index++)
        {
            var y = plotArea.Top + (plotArea.Height / horizontalDivisions * index);
            context.DrawLine(GridPen, new Point(plotArea.Left, y), new Point(plotArea.Right, y));
        }
    }

    private static void DrawAmbientHighlights(DrawingContext context, Rect plotArea)
    {
        var leftGlow = new Rect(plotArea.Left - 40, plotArea.Top + 30, 220, 220);
        var rightGlow = new Rect(plotArea.Right - 170, plotArea.Bottom - 180, 260, 260);

        context.DrawEllipse(new SolidColorBrush(Color.Parse("#103AE7D8")), null, leftGlow.Center, leftGlow.Width / 2, leftGlow.Height / 2);
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#0D9DF3FF")), null, rightGlow.Center, rightGlow.Width / 2, rightGlow.Height / 2);
    }

    private void DrawRoutes(DrawingContext context, Rect plotArea)
    {
        if (Routes.Count == 0)
        {
            return;
        }

        if (!TryGetProjectedBounds(out var minLatitude, out var maxLatitude, out var minLongitude, out var maxLongitude, out var longitudeScale))
        {
            return;
        }

        foreach (var route in Routes)
        {
            if (route.Points.Count < 2)
            {
                continue;
            }

            var geometry = new StreamGeometry();

            using (var geometryContext = geometry.Open())
            {
                var firstPoint = Project(route.Points[0], plotArea, minLatitude, maxLatitude, minLongitude, maxLongitude, longitudeScale);
                geometryContext.BeginFigure(firstPoint, false);

                for (var index = 1; index < route.Points.Count; index++)
                {
                    var point = Project(route.Points[index], plotArea, minLatitude, maxLatitude, minLongitude, maxLongitude, longitudeScale);
                    geometryContext.LineTo(point);
                }
            }

            context.DrawGeometry(null, GlowPen, geometry);
            context.DrawGeometry(null, SecondaryGlowPen, geometry);
            context.DrawGeometry(null, CorePen, geometry);

            var lastPoint = Project(route.Points[^1], plotArea, minLatitude, maxLatitude, minLongitude, maxLongitude, longitudeScale);
            context.DrawEllipse(HighlightBrush, null, lastPoint, 2.2, 2.2);
        }
    }

    private bool TryGetProjectedBounds(
        out double minLatitude,
        out double maxLatitude,
        out double minLongitude,
        out double maxLongitude,
        out double longitudeScale)
    {
        minLatitude = double.MaxValue;
        maxLatitude = double.MinValue;
        minLongitude = double.MaxValue;
        maxLongitude = double.MinValue;

        foreach (var point in Routes.SelectMany(route => route.Points))
        {
            minLatitude = Math.Min(minLatitude, point.Latitude);
            maxLatitude = Math.Max(maxLatitude, point.Latitude);
            minLongitude = Math.Min(minLongitude, point.Longitude);
            maxLongitude = Math.Max(maxLongitude, point.Longitude);
        }

        if (double.IsInfinity(minLatitude) || double.IsInfinity(maxLatitude) ||
            double.IsInfinity(minLongitude) || double.IsInfinity(maxLongitude) ||
            minLatitude == double.MaxValue || minLongitude == double.MaxValue)
        {
            longitudeScale = 1;
            return false;
        }

        var latitudePadding = Math.Max(0.01, (maxLatitude - minLatitude) * 0.12);
        var longitudePadding = Math.Max(0.01, (maxLongitude - minLongitude) * 0.12);
        minLatitude -= latitudePadding;
        maxLatitude += latitudePadding;
        minLongitude -= longitudePadding;
        maxLongitude += longitudePadding;

        var centerLatitude = (minLatitude + maxLatitude) / 2;
        longitudeScale = Math.Cos(centerLatitude * Math.PI / 180d);
        longitudeScale = Math.Abs(longitudeScale) < 0.01 ? 1 : longitudeScale;

        return true;
    }

    private static Point Project(
        HeatmapPointViewModel point,
        Rect plotArea,
        double minLatitude,
        double maxLatitude,
        double minLongitude,
        double maxLongitude,
        double longitudeScale)
    {
        var projectedMinLongitude = minLongitude * longitudeScale;
        var projectedMaxLongitude = maxLongitude * longitudeScale;
        var projectedLongitude = point.Longitude * longitudeScale;

        var xRatio = (projectedLongitude - projectedMinLongitude) / Math.Max(0.000001, projectedMaxLongitude - projectedMinLongitude);
        var yRatio = (point.Latitude - minLatitude) / Math.Max(0.000001, maxLatitude - minLatitude);

        return new Point(
            plotArea.Left + (plotArea.Width * xRatio),
            plotArea.Bottom - (plotArea.Height * yRatio));
    }
}
