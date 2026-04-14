namespace VeloCenter.Infrastructure.Integrations.Strava;

public static class StravaActivityFilter
{
    private static readonly HashSet<string> OutdoorCyclingSportTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ride",
        "GravelRide",
        "MountainBikeRide",
        "EBikeRide",
        "EMountainBikeRide",
        "Handcycle",
        "Velomobile",
    };

    public static bool IsOutdoorCyclingActivity(string? sportType, bool? trainer, string? legacyType = null)
    {
        if (trainer is true)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sportType))
        {
            return OutdoorCyclingSportTypes.Contains(sportType);
        }

        return !string.IsNullOrWhiteSpace(legacyType) &&
               OutdoorCyclingSportTypes.Contains(legacyType);
    }
}
