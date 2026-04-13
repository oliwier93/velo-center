namespace VeloCenter.App.ViewModels;

public sealed class NavigationItemViewModel : ViewModelBase
{
    private bool _isSelected;
    private bool _isExpanded = true;

    public NavigationItemViewModel(string key, string title, string description, string iconPathData)
    {
        Key = key;
        Title = title;
        Description = description;
        IconPathData = iconPathData;
    }

    public string Key { get; }

    public string Title { get; }

    public string Description { get; }

    public string IconPathData { get; }

    public System.Windows.Input.ICommand? ActivateCommand { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(IconBackground));
                OnPropertyChanged(nameof(IconBorderBrush));
                OnPropertyChanged(nameof(SelectionOpacity));
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(IsCollapsed));
            }
        }
    }

    public bool IsCollapsed => !IsExpanded;

    public string IconBackground => IsSelected ? "#523A1E68" : "#2617162E";

    public string IconBorderBrush => IsSelected ? "#B46CFF" : "#4E8A61AF";

    public double SelectionOpacity => IsSelected ? 1 : 0;
}
