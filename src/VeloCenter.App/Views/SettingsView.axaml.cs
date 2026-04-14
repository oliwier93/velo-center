using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
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

        var confirmed = await ShowResetConfirmationAsync(owner);

        if (!confirmed)
        {
            return;
        }

        await viewModel.ResetApplicationAsync();
    }

    private static async Task<bool> ShowResetConfirmationAsync(Window owner)
    {
        var confirmButton = new Button
        {
            Content = "Tak, wyczysc wszystko",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 148,
        };
        var cancelButton = new Button
        {
            Content = "Anuluj",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 96,
        };

        var dialog = new Window
        {
            Title = "Potwierdz reset",
            Width = 440,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Thickness(24),
                Child = new StackPanel
                {
                    Spacing = 18,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Tej operacji nie da sie cofnac.",
                            FontSize = 22,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new TextBlock
                        {
                            Text = "Czy na pewno chcesz usunac cala lokalna baze, integracje, sesje i stan aplikacji?",
                            FontSize = 14,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                cancelButton,
                                confirmButton,
                            },
                        },
                    },
                },
            },
        };

        confirmButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click += (_, _) => dialog.Close(false);

        return await dialog.ShowDialog<bool>(owner);
    }
}
