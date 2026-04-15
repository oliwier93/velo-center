namespace VeloCenter.App.ViewModels;

public sealed record HeatmapRouteViewModel(
    Guid ActivityId,
    string Title,
    string SourceLabel,
    DateTimeOffset StartTime,
    IReadOnlyList<HeatmapPointViewModel> Points);
