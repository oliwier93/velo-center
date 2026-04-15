using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using VeloCenter.Core.Activities;

namespace VeloCenter.App.ViewModels;

public sealed class ProgressViewModel : ViewModelBase
{
    private const double AxisWidthValue = 72;
    private const double MonthAxisHeightValue = 64;
    private const double PlotWidthValue = 860;
    private const double PlotHeightValue = 520;
    private static readonly string[] SeriesPalette =
    [
        "#FF6B6B",
        "#4D96FF",
        "#6BCB77",
        "#FFD166",
        "#F78FB3",
        "#00C2A8",
        "#845EC2",
        "#FF9671",
        "#2EC4B6",
        "#F94144",
        "#90BE6D",
        "#F8961E",
        "#43AA8B",
        "#277DA1",
        "#F3722C",
        "#B8DE6F",
    ];

    private IReadOnlyList<ProgressYearSeriesViewModel> _visibleAnnualSeries = [];

    public ProgressViewModel(IReadOnlyList<ActivitySummary> activities)
    {
        HasActivities = activities.Count > 0;
        PlotWidth = PlotWidthValue;
        PlotHeight = PlotHeightValue;
        MonthLabels = BuildMonthLabels();
        var today = DateTime.Today;

        if (!HasActivities)
        {
            Highlights =
            [
                new MetricTileViewModel("Lat na wykresie", "0", "Pierwszy import doda pierwszy rok do porownania."),
                new MetricTileViewModel("Suma kilometrow", "--", "Laczny dystans pojawi sie po pierwszym imporcie."),
            ];
            AnnualSeries = [];
            VisibleAnnualSeries = [];
            DistanceTicks = BuildDistanceTicks(new ChartScale(50, 10, 5));
            ChartSummary = "Po pierwszym imporcie zobaczysz tu porownanie kilometrow rok do roku.";
            return;
        }

        var yearlySnapshots = activities
            .GroupBy(activity => activity.StartTime.ToLocalTime().Date.Year)
            .OrderByDescending(group => group.Key)
            .Select((group, index) => CreateYearSnapshot(group.Key, group, today, SeriesPalette[index % SeriesPalette.Length]))
            .ToList();

        var chartScale = CalculateChartScale(yearlySnapshots.Max(snapshot => snapshot.TotalDistanceKm));
        DistanceTicks = BuildDistanceTicks(chartScale);
        AnnualSeries = yearlySnapshots
            .Select(snapshot => new ProgressYearSeriesViewModel(
                snapshot.Year,
                snapshot.Stroke,
                BuildPoints(
                    snapshot.DailyDistanceByDay,
                    snapshot.DaysInYear,
                    snapshot.VisibleThroughDayOfYear,
                    chartScale.UpperBoundKm),
                snapshot.TotalDistanceKm,
                snapshot.ActivitiesCount,
                snapshot.ShowEndMarker))
            .ToList();
        HookSeriesVisibility();
        UpdateVisibleAnnualSeries();

        var totalDistanceKm = AnnualSeries.Sum(series => series.TotalDistanceKm);

        Highlights =
        [
            new MetricTileViewModel("Lat na wykresie", AnnualSeries.Count.ToString(CultureInfo.InvariantCulture), "Kazdy rok z aktywnosciami ma osobna linie."),
            new MetricTileViewModel("Suma kilometrow", $"{totalDistanceKm:0} km", $"{activities.Count} treningow zapisanych w historii."),
        ];

        ChartSummary = "Kazda linia pokazuje narastajacy dystans w kolejnych dniach roku.";
    }

    public bool HasActivities { get; }

    public bool HasNoActivities => !HasActivities;

    public double PlotWidth { get; }

    public double PlotHeight { get; }

    public double AxisWidth => AxisWidthValue;

    public double MonthAxisHeight => MonthAxisHeightValue;

    public double ChartContentWidth => AxisWidthValue + PlotWidthValue;

    public double ChartContentHeight => PlotHeightValue + MonthAxisHeightValue;

    public string ChartSummary { get; }

    public IReadOnlyList<MetricTileViewModel> Highlights { get; }

    public IReadOnlyList<ProgressYearSeriesViewModel> AnnualSeries { get; }

    public IReadOnlyList<ProgressYearSeriesViewModel> VisibleAnnualSeries
    {
        get => _visibleAnnualSeries;
        private set => SetProperty(ref _visibleAnnualSeries, value);
    }

    public IReadOnlyList<ProgressAxisTickViewModel> DistanceTicks { get; }

    public IReadOnlyList<ProgressMonthLabelViewModel> MonthLabels { get; }

    private static YearlyProgressSnapshot CreateYearSnapshot(
        int year,
        IEnumerable<ActivitySummary> activities,
        DateTime today,
        string stroke)
    {
        var daysInYear = DateTime.IsLeapYear(year) ? 366 : 365;
        var dailyDistanceByDay = activities
            .GroupBy(activity => activity.StartTime.ToLocalTime().Date.DayOfYear)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(activity => activity.DistanceKm));

        var totalDistanceKm = dailyDistanceByDay.Values.Sum();

        return new YearlyProgressSnapshot(
            year,
            stroke,
            daysInYear,
            year == today.Year ? Math.Clamp(today.DayOfYear, 1, daysInYear) : daysInYear,
            dailyDistanceByDay,
            totalDistanceKm,
            activities.Count(),
            year == today.Year && today.DayOfYear < daysInYear);
    }

    private static IReadOnlyList<ProgressCumulativePointViewModel> BuildPoints(
        IReadOnlyDictionary<int, double> dailyDistanceByDay,
        int daysInYear,
        int visibleThroughDayOfYear,
        double chartMaxDistanceKm)
    {
        var points = new List<ProgressCumulativePointViewModel>(visibleThroughDayOfYear);
        var cumulativeDistanceKm = 0d;

        for (var dayOfYear = 1; dayOfYear <= visibleThroughDayOfYear; dayOfYear++)
        {
            cumulativeDistanceKm += dailyDistanceByDay.TryGetValue(dayOfYear, out var distanceKm)
                ? distanceKm
                : 0d;

            var x = ((dayOfYear - 1d) / Math.Max(1, daysInYear - 1d)) * PlotWidthValue;
            var y = PlotHeightValue - ((cumulativeDistanceKm / chartMaxDistanceKm) * PlotHeightValue);

            points.Add(new ProgressCumulativePointViewModel(
                dayOfYear,
                cumulativeDistanceKm,
                Math.Round(x, 2),
                Math.Round(y, 2)));
        }

        return points;
    }

    private static IReadOnlyList<ProgressAxisTickViewModel> BuildDistanceTicks(ChartScale chartScale)
    {
        var ticks = new List<ProgressAxisTickViewModel>();

        for (var index = chartScale.IntervalCount; index >= 0; index--)
        {
            var valueKm = chartScale.StepKm * index;
            var guideOffsetTop = PlotHeightValue - ((valueKm / chartScale.UpperBoundKm) * PlotHeightValue);
            var isTopBoundary = index == chartScale.IntervalCount;
            var isBottomBoundary = index == 0;
            var labelOffsetTop = guideOffsetTop - 10 + (isTopBoundary ? 6 : 0) + (isBottomBoundary ? -6 : 0);
            var showGuideLine = index > 0 && index < chartScale.IntervalCount;

            ticks.Add(new ProgressAxisTickViewModel(
                valueKm <= 0.01 ? "0 km" : $"{valueKm:0} km",
                Math.Round(guideOffsetTop, 2),
                Math.Round(labelOffsetTop, 2),
                showGuideLine));
        }

        return ticks;
    }

    private static IReadOnlyList<ProgressMonthLabelViewModel> BuildMonthLabels()
    {
        var labels = new List<ProgressMonthLabelViewModel>();
        string[] monthNames =
        [
            "sty",
            "lut",
            "mar",
            "kwi",
            "maj",
            "cze",
            "lip",
            "sie",
            "wrz",
            "paz",
            "lis",
            "gru",
        ];
        var monthStartDay = 1;

        for (var month = 1; month <= 12; month++)
        {
            var daysInMonth = DateTime.DaysInMonth(2024, month);
            var monthMiddleDay = monthStartDay + ((daysInMonth - 1) / 2d);
            var labelOffsetLeft = Math.Clamp((((monthMiddleDay - 1d) / 365d) * PlotWidthValue) - 14, 0, PlotWidthValue - 28);
            var guideOffsetLeft = ((monthStartDay - 1d) / 365d) * PlotWidthValue;

            labels.Add(new ProgressMonthLabelViewModel(
                monthNames[month - 1],
                Math.Round(guideOffsetLeft, 2),
                Math.Round(labelOffsetLeft, 2),
                month > 1));

            monthStartDay += daysInMonth;
        }

        return labels;
    }

    private static ChartScale CalculateChartScale(double maxDistanceKm)
    {
        if (maxDistanceKm <= 0)
        {
            return new ChartScale(50, 10, 5);
        }

        const int minimumIntervalCount = 5;
        var stepKm = CalculateNiceStep(maxDistanceKm / minimumIntervalCount);
        var intervalCount = Math.Max(minimumIntervalCount, (int)Math.Ceiling(maxDistanceKm / stepKm));

        return new ChartScale(stepKm * intervalCount, stepKm, intervalCount);
    }

    private static double CalculateNiceStep(double roughStepKm)
    {
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(roughStepKm)));
        var normalizedStep = roughStepKm / magnitude;
        var niceStep = normalizedStep switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 2.5 => 2.5,
            <= 4 => 4,
            <= 5 => 5,
            _ => 10,
        };

        return niceStep * magnitude;
    }

    private sealed record YearlyProgressSnapshot(
        int Year,
        string Stroke,
        int DaysInYear,
        int VisibleThroughDayOfYear,
        IReadOnlyDictionary<int, double> DailyDistanceByDay,
        double TotalDistanceKm,
        int ActivitiesCount,
        bool ShowEndMarker);

    private readonly record struct ChartScale(double UpperBoundKm, double StepKm, int IntervalCount);

    private void HookSeriesVisibility()
    {
        foreach (var series in AnnualSeries)
        {
            series.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ProgressYearSeriesViewModel.IsVisible))
                {
                    UpdateVisibleAnnualSeries();
                }
            };
        }
    }

    private void UpdateVisibleAnnualSeries()
    {
        VisibleAnnualSeries = AnnualSeries
            .Where(series => series.IsVisible)
            .OrderBy(series => series.Year)
            .ToList();
    }
}

public sealed class ProgressYearSeriesViewModel : ViewModelBase
{
    private bool _isVisible = true;

    public ProgressYearSeriesViewModel(
        int year,
        string stroke,
        IReadOnlyList<ProgressCumulativePointViewModel> points,
        double totalDistanceKm,
        int activitiesCount,
        bool showEndMarker)
    {
        Year = year;
        Stroke = stroke;
        Points = points;
        TotalDistanceKm = totalDistanceKm;
        ActivitiesCount = activitiesCount;
        ShowEndMarker = showEndMarker;
        PathData = BuildPathData(points);
        ToggleVisibilityCommand = new RelayCommand(() => IsVisible = !IsVisible);

        var lastPoint = points.LastOrDefault();
        EndPointLeft = lastPoint is null ? 0 : Math.Max(0, lastPoint.X - 4);
        EndPointTop = lastPoint is null ? 0 : Math.Max(0, lastPoint.Y - 4);
    }

    public int Year { get; }

    public string YearLabel => Year.ToString(CultureInfo.InvariantCulture);

    public string Stroke { get; }

    public IReadOnlyList<ProgressCumulativePointViewModel> Points { get; }

    public string PathData { get; }

    public double TotalDistanceKm { get; }

    public string TotalDistanceLabel => $"{TotalDistanceKm:0.0} km";

    public int ActivitiesCount { get; }

    public string ActivityCountLabel => $"{ActivitiesCount} treningow";

    public bool ShowEndMarker { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                OnPropertyChanged(nameof(LegendOpacity));
            }
        }
    }

    public double LegendOpacity => IsVisible ? 1 : 0.4;

    public IRelayCommand ToggleVisibilityCommand { get; }

    public double EndPointLeft { get; }

    public double EndPointTop { get; }

    private static string BuildPathData(IReadOnlyList<ProgressCumulativePointViewModel> points)
    {
        if (points.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];

            builder
                .Append(index == 0 ? "M " : " L ")
                .Append(point.X.ToString("0.##", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(point.Y.ToString("0.##", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}

public sealed class ProgressCumulativePointViewModel
{
    public ProgressCumulativePointViewModel(int dayOfYear, double cumulativeDistanceKm, double x, double y)
    {
        DayOfYear = dayOfYear;
        CumulativeDistanceKm = cumulativeDistanceKm;
        X = x;
        Y = y;
    }

    public int DayOfYear { get; }

    public double CumulativeDistanceKm { get; }

    public double X { get; }

    public double Y { get; }
}

public sealed class ProgressAxisTickViewModel
{
    public ProgressAxisTickViewModel(string label, double guideOffsetTop, double labelOffsetTop, bool showGuideLine)
    {
        Label = label;
        GuideOffsetTop = guideOffsetTop;
        LabelOffsetTop = labelOffsetTop;
        ShowGuideLine = showGuideLine;
    }

    public string Label { get; }

    public double GuideOffsetTop { get; }

    public double LabelOffsetTop { get; }

    public bool ShowGuideLine { get; }
}

public sealed class ProgressMonthLabelViewModel
{
    public ProgressMonthLabelViewModel(string label, double guideOffsetLeft, double labelOffsetLeft, bool showGuideLine)
    {
        Label = label;
        GuideOffsetLeft = guideOffsetLeft;
        LabelOffsetLeft = labelOffsetLeft;
        ShowGuideLine = showGuideLine;
    }

    public string Label { get; }

    public double GuideOffsetLeft { get; }

    public double LabelOffsetLeft { get; }

    public bool ShowGuideLine { get; }
}
