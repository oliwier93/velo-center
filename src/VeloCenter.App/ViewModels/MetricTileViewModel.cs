namespace VeloCenter.App.ViewModels;

public sealed class MetricTileViewModel
{
    public MetricTileViewModel(string label, string value, string detail)
    {
        Label = label;
        Value = value;
        Detail = detail;
    }

    public string Label { get; }

    public string Value { get; }

    public string Detail { get; }
}
