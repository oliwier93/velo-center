using System.Text.Json;
using VeloCenter.App.Models;
using VeloCenter.Infrastructure.Persistence;

namespace VeloCenter.App.Services;

public sealed class LocalActivityRangePreferencesStore : IActivityRangePreferencesStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _preferencesPath;

    public LocalActivityRangePreferencesStore()
        : this(Path.Combine(VeloCenterSqliteDatabase.GetApplicationDataDirectory(), "ui-preferences.json"))
    {
    }

    public LocalActivityRangePreferencesStore(string preferencesPath)
    {
        _preferencesPath = preferencesPath;
    }

    public ActivityRangeSelection Load()
    {
        try
        {
            if (!File.Exists(_preferencesPath))
            {
                return ActivityRangeSelection.Default;
            }

            var payload = JsonSerializer.Deserialize<ActivityRangePreferencesPayload>(
                File.ReadAllText(_preferencesPath),
                SerializerOptions);

            if (payload is null || !Enum.TryParse<ActivityRangePreset>(payload.Preset, out var preset))
            {
                return ActivityRangeSelection.Default;
            }

            return new ActivityRangeSelection(preset, payload.StartDate?.Date, payload.EndDate?.Date);
        }
        catch
        {
            return ActivityRangeSelection.Default;
        }
    }

    public void Save(ActivityRangeSelection selection)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_preferencesPath)!);

        var payload = new ActivityRangePreferencesPayload
        {
            Preset = selection.Preset.ToString(),
            StartDate = selection.StartDate?.Date,
            EndDate = selection.EndDate?.Date,
        };

        File.WriteAllText(_preferencesPath, JsonSerializer.Serialize(payload, SerializerOptions));
    }

    private sealed class ActivityRangePreferencesPayload
    {
        public string Preset { get; set; } = ActivityRangePreset.Last30Days.ToString();

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }
    }
}
