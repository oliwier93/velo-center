namespace VeloCenter.App.ViewModels;

public sealed class ImportViewModel : ViewModelBase
{
    private string _importStatus = string.Empty;

    public string AcceptedExtensionsLabel { get; } = "Akceptowane: .fit, .fit.gz, .gpx";

    public string ImportStatus
    {
        get => _importStatus;
        private set
        {
            if (SetProperty(ref _importStatus, value))
            {
                OnPropertyChanged(nameof(HasImportStatus));
            }
        }
    }

    public bool HasImportStatus => !string.IsNullOrWhiteSpace(ImportStatus);

    public void SetImportQueued(string filePath)
    {
        ImportStatus = $"Importowanie: {System.IO.Path.GetFileName(filePath)}";
    }

    public void SetImportBlocked(string reason)
    {
        ImportStatus = reason;
    }

    public void SetImportCompleted(VeloCenter.Core.Activities.ActivityImportResult result)
    {
        var actionLabel = result.WasCreated ? "Zapisano" : "Zaktualizowano";
        ImportStatus = $"{actionLabel}: {result.Activity.Title}";
    }

    public void SetImportFailed(string reason)
    {
        ImportStatus = $"Blad importu: {reason}";
    }
}
