using Avalonia.Controls;
using Avalonia.Interactivity;

namespace VeloCenter.App.Views;

public readonly record struct CustomDateRangeResult(DateTime StartDate, DateTime EndDate);

public partial class CustomDateRangeWindow : Window
{
    public CustomDateRangeWindow()
        : this(DateTime.Today.AddDays(-29), DateTime.Today)
    {
    }

    public CustomDateRangeWindow(DateTime startDate, DateTime endDate)
    {
        InitializeComponent();
        StartDatePicker.SelectedDate = startDate.Date;
        EndDatePicker.SelectedDate = endDate.Date;
    }

    private void ApplyClick(object? sender, RoutedEventArgs e)
    {
        var startDate = StartDatePicker.SelectedDate?.Date;
        var endDate = EndDatePicker.SelectedDate?.Date;

        if (startDate is null || endDate is null)
        {
            ShowValidation("Wybierz obie daty zakresu.");
            return;
        }

        if (endDate < startDate)
        {
            ShowValidation("Data koncowa nie moze byc wczesniejsza niz poczatkowa.");
            return;
        }

        Close(new CustomDateRangeResult(startDate.Value, endDate.Value));
    }

    private void CancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationBorder.IsVisible = true;
    }
}
