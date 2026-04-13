namespace VeloCenter.App.ViewModels;

public sealed class InfoCardViewModel
{
    public InfoCardViewModel(string title, string value, string description)
    {
        Title = title;
        Value = value;
        Description = description;
    }

    public string Title { get; }

    public string Value { get; }

    public string Description { get; }
}
