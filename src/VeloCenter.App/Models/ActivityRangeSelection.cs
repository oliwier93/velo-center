namespace VeloCenter.App.Models;

public sealed record ActivityRangeSelection(
    ActivityRangePreset Preset,
    DateTime? StartDate = null,
    DateTime? EndDate = null)
{
    public static ActivityRangeSelection Default { get; } = new(ActivityRangePreset.Last30Days);
}
