using System.Globalization;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using VeloCenter.Core.Activities;
using VeloCenter.Infrastructure.Persistence;

namespace VeloCenter.Infrastructure.Activities;

public sealed class LocalFileActivityImportService(string databasePath) : IActivityImportService
{
    private readonly DbContextOptions<VeloCenterDbContext> _dbContextOptions = VeloCenterSqliteDatabase.CreateOptions(databasePath);

    public ActivityImportResult ImportLocalFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var normalizedPath = Path.GetFullPath(filePath);

        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("Nie znaleziono pliku do importu.", normalizedPath);
        }

        var draft = CreateDraft(normalizedPath);
        var importedAt = DateTimeOffset.UtcNow;

        using var dbContext = new VeloCenterDbContext(_dbContextOptions);

        var record = dbContext.Activities.SingleOrDefault(activity =>
            activity.Source == draft.Source &&
            activity.ImportFingerprint == draft.ImportFingerprint);
        var wasCreated = record is null;

        if (record is null)
        {
            record = new ActivityRecord
            {
                Id = Guid.NewGuid(),
                Source = draft.Source,
                ImportFingerprint = draft.ImportFingerprint,
                ImportedAt = importedAt,
            };

            dbContext.Add(record);
        }

        record.Title = draft.Title;
        record.StartTime = draft.StartTime;
        record.DistanceKm = draft.DistanceKm;
        record.DurationSeconds = Math.Max(0, (int)Math.Round(draft.Duration.TotalSeconds));
        record.LastUpdatedAt = importedAt;

        dbContext.SaveChanges();

        return new ActivityImportResult(
            new ActivitySummary(
                record.Id,
                record.Source,
                record.Title,
                record.StartTime,
                record.DistanceKm,
                TimeSpan.FromSeconds(record.DurationSeconds)),
            wasCreated);
    }

    private static ImportedActivityDraft CreateDraft(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var source = ResolveSource(fileName);
        var fingerprint = ComputeFingerprint(filePath, source);

        return source switch
        {
            ActivitySource.GpxFile => CreateGpxDraft(filePath, fingerprint),
            ActivitySource.FitFile => CreateFitDraft(filePath, fingerprint),
            _ => throw new InvalidDataException("Obslugiwane sa tylko pliki FIT i GPX."),
        };
    }

    private static ImportedActivityDraft CreateFitDraft(string filePath, string fingerprint)
    {
        var title = GetDisplayTitle(filePath);
        var startTime = GetFileTimestamp(filePath);

        return new ImportedActivityDraft(
            ActivitySource.FitFile,
            title,
            startTime,
            0,
            TimeSpan.Zero,
            fingerprint);
    }

    private static ImportedActivityDraft CreateGpxDraft(string filePath, string fingerprint)
    {
        try
        {
            var document = XDocument.Load(filePath, LoadOptions.None);
            var root = document.Root ?? throw new InvalidDataException("Plik GPX nie ma poprawnego wezla glownego.");
            var ns = root.Name.Namespace;
            var title = document
                .Descendants(ns + "trk")
                .Elements(ns + "name")
                .Select(element => element.Value.Trim())
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                ?? document
                    .Descendants(ns + "metadata")
                    .Elements(ns + "name")
                    .Select(element => element.Value.Trim())
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                ?? GetDisplayTitle(filePath);

            var trackPoints = document
                .Descendants(ns + "trkpt")
                .Select(element => new GpxTrackPoint(
                    ParseRequiredDouble(element.Attribute("lat")?.Value, "lat"),
                    ParseRequiredDouble(element.Attribute("lon")?.Value, "lon"),
                    ParseOptionalTime(element.Element(ns + "time")?.Value)))
                .ToList();

            if (trackPoints.Count == 0)
            {
                return new ImportedActivityDraft(
                    ActivitySource.GpxFile,
                    title,
                    GetFileTimestamp(filePath),
                    0,
                    TimeSpan.Zero,
                    fingerprint);
            }

            var timestamps = trackPoints
                .Where(point => point.Time is not null)
                .Select(point => point.Time!.Value)
                .ToList();
            var startTime = timestamps.Count > 0
                ? timestamps.Min()
                : GetFileTimestamp(filePath);
            var duration = timestamps.Count > 1
                ? timestamps.Max() - timestamps.Min()
                : TimeSpan.Zero;
            var distanceKm = CalculateDistanceKm(trackPoints);

            return new ImportedActivityDraft(
                ActivitySource.GpxFile,
                title,
                startTime,
                distanceKm,
                duration,
                fingerprint);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("Nie udalo sie odczytac pliku GPX.", exception);
        }
    }

    private static string ComputeFingerprint(string filePath, ActivitySource source)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);

        return $"{source}:{Convert.ToHexString(hash)}";
    }

    private static ActivitySource ResolveSource(string fileName)
    {
        if (fileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
        {
            return ActivitySource.GpxFile;
        }

        if (fileName.EndsWith(".fit", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".fit.gz", StringComparison.OrdinalIgnoreCase))
        {
            return ActivitySource.FitFile;
        }

        throw new InvalidDataException("Obslugiwane sa tylko pliki FIT i GPX.");
    }

    private static string GetDisplayTitle(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        return fileName.EndsWith(".fit.gz", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(fileName))
            : Path.GetFileNameWithoutExtension(fileName);
    }

    private static DateTimeOffset GetFileTimestamp(string filePath) => new(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero);

    private static double ParseRequiredDouble(string? value, string attributeName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue))
        {
            throw new InvalidDataException($"Niepoprawna wartosc atrybutu {attributeName} w pliku GPX.");
        }

        return parsedValue;
    }

    private static DateTimeOffset? ParseOptionalTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    private static double CalculateDistanceKm(IReadOnlyList<GpxTrackPoint> trackPoints)
    {
        var totalMeters = 0d;

        for (var index = 1; index < trackPoints.Count; index++)
        {
            totalMeters += CalculateDistanceMeters(trackPoints[index - 1], trackPoints[index]);
        }

        return totalMeters / 1000d;
    }

    private static double CalculateDistanceMeters(GpxTrackPoint start, GpxTrackPoint end)
    {
        const double earthRadiusMeters = 6_371_000d;
        var startLatitude = DegreesToRadians(start.Latitude);
        var endLatitude = DegreesToRadians(end.Latitude);
        var latitudeDelta = DegreesToRadians(end.Latitude - start.Latitude);
        var longitudeDelta = DegreesToRadians(end.Longitude - start.Longitude);

        var a =
            Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2) +
            Math.Cos(startLatitude) * Math.Cos(endLatitude) *
            Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180d);

    private sealed record ImportedActivityDraft(
        ActivitySource Source,
        string Title,
        DateTimeOffset StartTime,
        double DistanceKm,
        TimeSpan Duration,
        string ImportFingerprint);

    private sealed record GpxTrackPoint(
        double Latitude,
        double Longitude,
        DateTimeOffset? Time);
}
