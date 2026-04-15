using Avalonia.Controls;
using Avalonia.Interactivity;

namespace VeloCenter.App.Views;

public partial class ResetConfirmationWindow : Window
{
    public ResetConfirmationWindow()
    {
        InitializeComponent();
    }

    private void ConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void CancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
