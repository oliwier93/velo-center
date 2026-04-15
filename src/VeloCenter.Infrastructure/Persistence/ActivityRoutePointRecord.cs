namespace VeloCenter.Infrastructure.Persistence;

internal sealed class ActivityRoutePointRecord
{
    public Guid ActivityId { get; set; }

    public int Sequence { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public ActivityRecord Activity { get; set; } = null!;
}
