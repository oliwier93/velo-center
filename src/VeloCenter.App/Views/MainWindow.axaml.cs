using Avalonia.Controls;
using Avalonia.Input;
using VeloCenter.App.ViewModels;

namespace VeloCenter.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
}
