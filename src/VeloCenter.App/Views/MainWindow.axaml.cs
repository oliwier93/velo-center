using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.ComponentModel;
using VeloCenter.App.Models;
using VeloCenter.App.ViewModels;

namespace VeloCenter.App.Views;

public partial class MainWindow : Window
{
    private bool _isRangeSelectorReady;
    private bool _isApplyingRangeSelection;
    private MainWindowViewModel? _mainWindowViewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Opened += (_, _) =>
        {
            _isRangeSelectorReady = true;
            UpdateSectionHostLayout();
        };
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

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_mainWindowViewModel is not null)
        {
            _mainWindowViewModel.PropertyChanged -= OnMainWindowViewModelPropertyChanged;
        }

        _mainWindowViewModel = DataContext as MainWindowViewModel;

        if (_mainWindowViewModel is not null)
        {
            _mainWindowViewModel.PropertyChanged += OnMainWindowViewModelPropertyChanged;
        }

        UpdateSectionHostLayout();
    }

    private void OnMainWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.CurrentSectionViewModel) or nameof(MainWindowViewModel.UseViewportSectionLayout))
        {
            UpdateSectionHostLayout();
        }
    }

    private void UpdateSectionHostLayout()
    {
        if (_mainWindowViewModel is null)
        {
            StandardSectionHost.Content = null;
            StandardSectionScrollViewer.IsVisible = false;
            ViewportSectionHost.Content = null;
            ViewportSectionHostContainer.IsVisible = false;
            return;
        }

        if (_mainWindowViewModel.UseViewportSectionLayout)
        {
            StandardSectionHost.Content = null;
            StandardSectionScrollViewer.IsVisible = false;
            ViewportSectionHost.Content = _mainWindowViewModel.CurrentSectionViewModel;
            ViewportSectionHostContainer.IsVisible = true;
            return;
        }

        ViewportSectionHost.Content = null;
        ViewportSectionHostContainer.IsVisible = false;
        StandardSectionHost.Content = _mainWindowViewModel.CurrentSectionViewModel;
        StandardSectionScrollViewer.IsVisible = true;
    }
}
