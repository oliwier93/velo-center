using Avalonia.Controls;
using Avalonia.Interactivity;
using VeloCenter.App.ViewModels;

namespace VeloCenter.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void ResetApplicationClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner ||
            owner.DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var dialog = new ResetConfirmationWindow();
        var confirmed = await dialog.ShowDialog<bool>(owner);

        if (!confirmed)
        {
            return;
        }

        await viewModel.ResetApplicationAsync();
    }
}
