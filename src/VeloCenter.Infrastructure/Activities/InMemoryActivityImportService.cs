using VeloCenter.Core.Activities;

namespace VeloCenter.Infrastructure.Activities;

public sealed class InMemoryActivityImportService : IActivityImportService
{
    public ActivityImportResult ImportLocalFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fileName = Path.GetFileName(filePath);
        var source = fileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase)
            ? ActivitySource.GpxFile
            : ActivitySource.FitFile;
        var title = fileName.EndsWith(".fit.gz", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(fileName))
            : Path.GetFileNameWithoutExtension(fileName);

        return new ActivityImportResult(
            new ActivitySummary(
                Guid.NewGuid(),
                source,
                title,
                DateTimeOffset.Now,
                0,
                TimeSpan.Zero),
            true);
    }
}
