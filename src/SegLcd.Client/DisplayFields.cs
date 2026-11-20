using System.Globalization;

using SegLcd.Localization;

namespace SegLcd.Client;

/// <summary>A value the service sends to the display.</summary>
/// <param name="Name">Field name as on D-Bus (<c>cpu_temperature</c>, …).</param>
/// <param name="SensorKind">Kind of sensor that can be assigned (D-Bus <c>ListSensors</c>), <c>null</c> = not configurable.</param>
/// <param name="Unit">Unit shown after the value.</param>
/// <param name="Decimals">Decimal places shown.</param>
/// <param name="ShownByLb360S">The LB360-S displays this value.</param>
public sealed record DisplayField(string Name, string? SensorKind, string Unit, int Decimals, bool ShownByLb360S)
{
    /// <summary>Localized title.</summary>
    public string Title => Name switch
    {
        "cpu_temperature" => Texts.Current.T("CPU temperature"),
        "cpu_usage" => Texts.Current.T("CPU usage"),
        "cpu_power" => Texts.Current.T("CPU power"),
        "cpu_clock" => Texts.Current.T("CPU clock"),
        "cpu_voltage" => Texts.Current.T("CPU voltage"),
        "gpu_temperature" => Texts.Current.T("GPU temperature"),
        "gpu_usage" => Texts.Current.T("GPU usage"),
        "gpu_power" => Texts.Current.T("GPU power"),
        "gpu_clock" => Texts.Current.T("GPU clock"),
        "fan" => Texts.Current.T("Fan"),
        "pump" => Texts.Current.T("Pump"),
        "ram_usage" => Texts.Current.T("RAM usage"),
        _ => Name,
    };

    /// <summary>The value with unit, or "–" if missing.</summary>
    public string Format(double? value) =>
        value is { } v && double.IsFinite(v)
            ? v.ToString("F" + Decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture) + " " + Unit
            : "–";
}

/// <summary>All display fields in display order.</summary>
public static class DisplayFields
{
    public static IReadOnlyList<DisplayField> All { get; } =
    [
        new("cpu_temperature", "temperature", "°C", 1, true),
        new("cpu_usage", null, "%", 0, false),
        new("cpu_power", "power", "W", 1, false),
        new("cpu_clock", null, "MHz", 0, false),
        new("cpu_voltage", "voltage", "V", 3, false),
        new("gpu_temperature", "temperature", "°C", 1, true),
        new("gpu_usage", "usage", "%", 0, false),
        new("gpu_power", "power", "W", 1, false),
        new("gpu_clock", "clock", "MHz", 0, false),
        new("fan", "fan", "RPM", 0, false),
        new("pump", "fan", "RPM", 0, false),
        new("ram_usage", null, "%", 0, false),
    ];

    /// <summary>Fields with a configurable sensor (D-Bus <c>SetSensor</c>).</summary>
    public static IReadOnlyList<DisplayField> Configurable { get; } = [.. All.Where(f => f.SensorKind is not null)];
}

/// <summary>One entry of a sensor selection list.</summary>
/// <param name="Spec">Spec for <c>SetSensor</c>; empty = automatic.</param>
/// <param name="Title">Main line.</param>
/// <param name="Subtitle">Current value and source.</param>
public sealed record SensorChoice(string Spec, string Title, string Subtitle);

/// <summary>Builds the selection list of a field.</summary>
public static class SensorChoices
{
    /// <summary>
    /// "Automatic" first, then the sensors of the field's kind, sorted by spec. A configured spec that is not in the list
    /// (sum, hwmonN path, sensor gone) is kept as its own entry, so the selection always shows the configuration.
    /// </summary>
    public static IReadOnlyList<SensorChoice> For(DisplayField field, IEnumerable<ServiceSensor> sensors, string configuredSpec)
    {
        var choices = new List<SensorChoice> { new(string.Empty, Texts.Current.T("Automatic"), Texts.Current.T("first available default")) };
        choices.AddRange(sensors
            .Where(s => s.Kind == field.SensorKind)
            .DistinctBy(s => s.Spec)
            .OrderBy(s => s.Spec, StringComparer.Ordinal)
            .Select(s => new SensorChoice(s.Spec, s.Spec, $"{field.Format(s.Value)} · {s.Description}")));
        if (configuredSpec.Length > 0 && choices.All(c => c.Spec != configuredSpec))
            choices.Add(new(configuredSpec, configuredSpec, Texts.Current.T("configured, not in the list")));
        return choices;
    }

    /// <summary>Index of <paramref name="spec"/> in <paramref name="choices"/>; 0 (automatic) if missing.</summary>
    public static int IndexOf(IReadOnlyList<SensorChoice> choices, string spec)
    {
        for (var i = 0; i < choices.Count; i++)
        {
            if (choices[i].Spec == spec)
                return i;
        }

        return 0;
    }
}
