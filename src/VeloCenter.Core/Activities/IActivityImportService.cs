namespace VeloCenter.Core.Activities;

public interface IActivityImportService
{
    ActivityImportResult ImportLocalFile(string filePath);
}
