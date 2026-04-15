using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VeloCenter.App.Models;
using VeloCenter.App.ViewModels;

namespace VeloCenter.App.Views;

public partial class MainWindow : Window
{
    private bool _isRangeSelectorReady;
    private bool _isApplyingRangeSelection;

    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => _isRangeSelectorReady = true;
    }

    private void SidebarPointerEntered(object? sender, PointerEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetSidebarHoverState(true);
        }
    }

    private void SidebarPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetSidebarHoverState(false);
        }
    }

    private async void RangeSelectorSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isRangeSelectorReady || _isApplyingRangeSelection)
        {
            return;
        }

        if (sender is not ComboBox comboBox ||
            comboBox.SelectedItem is not ActivityRangeOptionViewModel selectedOption ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        _isApplyingRangeSelection = true;

        try
        {
            if (selectedOption.Preset is ActivityRangePreset.Custom)
            {
                var (startDate, endDate) = viewModel.GetCustomRangeDraft();
                var dialog = new CustomDateRangeWindow(startDate, endDate);
                var result = await dialog.ShowDialog<CustomDateRangeResult?>(this);

                if (result is { } customRange)
                {
                    viewModel.ApplyCustomRange(customRange.StartDate, customRange.EndDate);
                }
                else
                {
                    viewModel.RestoreCurrentRangeSelection();
                }
            }
            else
            {
                viewModel.ApplyPresetRange(selectedOption.Preset);
            }

            comboBox.SelectedItem = viewModel.SelectedRangeOption;
        }
        finally
        {
            _isApplyingRangeSelection = false;
        }
    }
}
