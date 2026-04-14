using VeloCenter.Core.Activities;

namespace VeloCenter.Infrastructure.Persistence;

internal sealed class ActivityRecord
{
    public Guid Id { get; set; }

    public ActivitySource Source { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset StartTime { get; set; }

    public double DistanceKm { get; set; }

    public int DurationSeconds { get; set; }
}
