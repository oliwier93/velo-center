using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VeloCenter.App.ViewModels;

namespace VeloCenter.App.Views;

public partial class ImportView : UserControl
{
    public ImportView()
    {
        InitializeComponent();
    }

    private async void PickFileClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel?.StorageProvider is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Wybierz plik aktywnosci",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Pliki aktywnosci")
                    {
                        Patterns = ["*.fit", "*.fit.gz", "*.gpx"],
                    },
                ],
            });

        var filePath = files.FirstOrDefault()?.TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(filePath) ||
            topLevel is not Window { DataContext: MainWindowViewModel viewModel })
        {
            return;
        }

        await viewModel.StartFileImportAsync(filePath);
    }

    private async void DisconnectStravaClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window { DataContext: MainWindowViewModel viewModel })
        {
            return;
        }

        await viewModel.DisconnectStravaAsync();
    }

    private async void SyncStravaClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window { DataContext: MainWindowViewModel viewModel })
        {
            return;
        }

        await viewModel.RunStravaPrimaryActionAsync();
    }
}
