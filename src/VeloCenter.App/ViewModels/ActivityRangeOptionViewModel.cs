using VeloCenter.App.Models;

namespace VeloCenter.App.ViewModels;

public sealed class ActivityRangeOptionViewModel(ActivityRangePreset preset, string label) : ViewModelBase
{
    private string _label = label;

    public ActivityRangePreset Preset { get; } = preset;

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }
}
