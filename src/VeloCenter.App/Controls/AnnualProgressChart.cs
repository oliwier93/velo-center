using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VeloCenter.App.ViewModels;

namespace VeloCenter.App.Controls;

public sealed class AnnualProgressChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<ProgressYearSeriesViewModel>?> AnnualSeriesProperty =
        AvaloniaProperty.Register<AnnualProgressChart, IReadOnlyList<ProgressYearSeriesViewModel>?>(nameof(AnnualSeries));

    public static readonly StyledProperty<IReadOnlyList<ProgressAxisTickViewModel>?> DistanceTicksProperty =
        AvaloniaProperty.Register<AnnualProgressChart, IReadOnlyList<ProgressAxisTickViewModel>?>(nameof(DistanceTicks));

    public static readonly StyledProperty<IReadOnlyList<ProgressMonthLabelViewModel>?> MonthLabelsProperty =
        AvaloniaProperty.Register<AnnualProgressChart, IReadOnlyList<ProgressMonthLabelViewModel>?>(nameof(MonthLabels));

    public static readonly StyledProperty<double> PlotWidthReferenceProperty =
        AvaloniaProperty.Register<AnnualProgressChart, double>(nameof(PlotWidthReference), 860d);

    public static readonly StyledProperty<double> PlotHeightReferenceProperty =
        AvaloniaProperty.Register<AnnualProgressChart, double>(nameof(PlotHeightReference), 260d);

    public static readonly StyledProperty<double> AxisWidthReferenceProperty =
        AvaloniaProperty.Register<AnnualProgressChart, double>(nameof(AxisWidthReference), 72d);

    public static readonly StyledProperty<double> MonthAxisHeightReferenceProperty =
        AvaloniaProperty.Register<AnnualProgressChart, double>(nameof(MonthAxisHeightReference), 64d);

    static AnnualProgressChart()
    {
        AffectsRender<AnnualProgressChart>(
            AnnualSeriesProperty,
            DistanceTicksProperty,
            MonthLabelsProperty,
            PlotWidthReferenceProperty,
            PlotHeightReferenceProperty,
            AxisWidthReferenceProperty,
            MonthAxisHeightReferenceProperty);
    }

    public IReadOnlyList<ProgressYearSeriesViewModel>? AnnualSeries
    {
        get => GetValue(AnnualSeriesProperty);
        set => SetValue(AnnualSeriesProperty, value);
    }

    public IReadOnlyList<ProgressAxisTickViewModel>? DistanceTicks
    {
        get => GetValue(DistanceTicksProperty);
        set => SetValue(DistanceTicksProperty, value);
    }

    public IReadOnlyList<ProgressMonthLabelViewModel>? MonthLabels
    {
        get => GetValue(MonthLabelsProperty);
        set => SetValue(MonthLabelsProperty, value);
    }

    public double PlotWidthReference
    {
        get => GetValue(PlotWidthReferenceProperty);
        set => SetValue(PlotWidthReferenceProperty, value);
    }

    public double PlotHeightReference
    {
        get => GetValue(PlotHeightReferenceProperty);
        set => SetValue(PlotHeightReferenceProperty, value);
    }

    public double AxisWidthReference
    {
        get => GetValue(AxisWidthReferenceProperty);
        set => SetValue(AxisWidthReferenceProperty, value);
    }

    public double MonthAxisHeightReference
    {
        get => GetValue(MonthAxisHeightReferenceProperty);
        set => SetValue(MonthAxisHeightReferenceProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var axisWidth = Math.Min(AxisWidthReference, Math.Max(56, Bounds.Width * 0.16));
        var monthAxisHeight = Math.Min(MonthAxisHeightReference, Math.Max(48, Bounds.Height * 0.16));
        const double topPadding = 10;
        const double bottomPadding = 6;
        var plotRect = new Rect(
            axisWidth,
            topPadding,
            Math.Max(0, Bounds.Width - axisWidth),
            Math.Max(0, Bounds.Height - monthAxisHeight - topPadding - bottomPadding));

        if (plotRect.Width <= 0 || plotRect.Height <= 0)
        {
            return;
        }

        var plotWidthReference = Math.Max(1, PlotWidthReference);
        var plotHeightReference = Math.Max(1, PlotHeightReference);
        var xScale = plotRect.Width / plotWidthReference;
        var yScale = plotRect.Height / plotHeightReference;

        var panelBackgroundBrush = ResolveBrush("PanelBackgroundBrush", "#101723");
        var panelStrokeBrush = ResolveBrush("PanelStrokeBrush", "#3B4658");
        var textSubtleBrush = ResolveBrush("TextSubtleBrush", "#A2B0C6");
        var monthGuideBrush = ResolveBrush("TextSubtleBrush", "#8FA4C7", 0.42);
        var distanceGuideBrush = ResolveBrush("TextSubtleBrush", "#AFC1DD", 0.34);

        var axisPen = new Pen(panelStrokeBrush, 1.3);
        var monthGuidePen = new Pen(monthGuideBrush, 1.2, dashStyle: new DashStyle([4, 4], 0));
        var distanceGuidePen = new Pen(distanceGuideBrush, 1.2, dashStyle: new DashStyle([4, 4], 0));

        context.DrawRectangle(panelBackgroundBrush, axisPen, plotRect);

        var distanceTicks = DistanceTicks ?? [];
        foreach (var tick in distanceTicks)
        {
            var y = plotRect.Top + (tick.GuideOffsetTop * yScale);
            if (tick.ShowGuideLine)
            {
                context.DrawLine(distanceGuidePen, new Point(plotRect.Left, y), new Point(plotRect.Right, y));
            }

            var label = CreateText(tick.Label, 12, textSubtleBrush, FontWeight.SemiBold);
            var labelX = Math.Max(0, axisWidth - label.Width - 8);
            var labelY = plotRect.Top + (tick.LabelOffsetTop * yScale);
            context.DrawText(label, new Point(labelX, labelY));
        }

        var monthLabels = MonthLabels ?? [];
        foreach (var month in monthLabels)
        {
            var x = plotRect.Left + (month.GuideOffsetLeft * xScale);
            if (month.ShowGuideLine)
            {
                context.DrawLine(monthGuidePen, new Point(x, plotRect.Top), new Point(x, plotRect.Bottom));
                context.DrawLine(axisPen, new Point(x, plotRect.Bottom), new Point(x, plotRect.Bottom + 8));
            }

            var label = CreateText(month.Label, 12, textSubtleBrush, FontWeight.SemiBold);
            var monthCenterX = plotRect.Left + ((month.LabelOffsetLeft + 14) * xScale);
            var labelX = monthCenterX - (label.Width / 2);
            context.DrawText(label, new Point(labelX, plotRect.Bottom + 12));
        }

        var seriesCollection = AnnualSeries ?? [];
        using (context.PushClip(plotRect))
        {
            foreach (var series in seriesCollection)
            {
                if (series.Points.Count < 2)
                {
                    continue;
                }

                var seriesBrush = new SolidColorBrush(Color.Parse(series.Stroke));
                var seriesPen = new Pen(seriesBrush, 2);

                for (var index = 1; index < series.Points.Count; index++)
                {
                    var start = ToPlotPoint(plotRect, series.Points[index - 1], xScale, yScale);
                    var end = ToPlotPoint(plotRect, series.Points[index], xScale, yScale);
                    context.DrawLine(seriesPen, start, end);
                }

                if (series.ShowEndMarker)
                {
                    var lastPoint = series.Points[^1];
                    var endPoint = ToPlotPoint(plotRect, lastPoint, xScale, yScale);
                    context.DrawEllipse(seriesBrush, null, endPoint, 3, 3);
                }
            }
        }
    }

    private static Point ToPlotPoint(Rect plotRect, ProgressCumulativePointViewModel point, double xScale, double yScale)
    {
        return new Point(
            plotRect.Left + (point.X * xScale),
            plotRect.Top + (point.Y * yScale));
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

    private IBrush ResolveBrush(string resourceKey, string fallbackHex, double? fallbackOpacity = null)
    {
        if (TryGetResource(resourceKey, ActualThemeVariant, out var value) && value is IBrush brush)
        {
            return brush;
        }

        var fallbackBrush = new SolidColorBrush(Color.Parse(fallbackHex));

        if (fallbackOpacity.HasValue)
        {
            fallbackBrush.Opacity = fallbackOpacity.Value;
        }

        return fallbackBrush;
    }
}
