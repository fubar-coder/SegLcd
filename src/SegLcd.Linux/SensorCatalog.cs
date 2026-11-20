using System.Globalization;

namespace SegLcd.Linux;

/// <summary>What a sensor measures; decides which fields it can be assigned to.</summary>
public enum SensorKind
{
    Temperature,
    Power,
    Voltage,
    Current,
    Clock,
    Usage,
    Fan,
    Other,
}

/// <summary>A sensor that can be configured, with its spec and current value (<c>null</c> = not readable).</summary>
public sealed record AvailableSensor(string Spec, SensorKind Kind, string Description, double? Value);

/// <summary>All configurable sensors of this system: hwmon, RAPL, NVML and DRM (the same sources as <c>seglcd sensors</c>).</summary>
public static class SensorCatalog
{
    /// <summary>
    /// Lists the sensors. RAPL power needs two readings: the method waits <paramref name="raplWindow"/> between them
    /// (only if RAPL domains exist).
    /// </summary>
    public static async Task<IReadOnlyList<AvailableSensor>> EnumerateAsync(TimeSpan raplWindow, CancellationToken cancellationToken = default)
    {
        var result = new List<AvailableSensor>();
        // chip:label is stable across reboots, but only unique if no second chip of that name has the label
        // (e.g. two NVMe drives); then the hwmon path is the spec.
        var hwmon = LinuxSensors.EnumerateHwmon();
        var ambiguous = hwmon.GroupBy(s => $"{s.Chip}:{s.Label}").Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        foreach (var s in hwmon)
        {
            var spec = $"{s.Chip}:{s.Label}";
            result.Add(new(ambiguous.Contains(spec) ? s.Id : spec, HwmonKind(s.Kind), ambiguous.Contains(spec) ? spec : s.Id, s.Read()));
        }

        var rapl = RaplPowerMeter.EnumerateDomains()
            .Select(d => (Spec: "rapl:" + d.Name.Split('-')[0], d.Dir, Meter: RaplPowerMeter.TryCreate(d.Name)))
            .ToList();
        if (rapl.Count > 0)
            await Task.Delay(raplWindow, cancellationToken).ConfigureAwait(false);
        foreach (var (spec, dir, meter) in rapl)
            result.Add(new(spec, SensorKind.Power, dir, meter?.Read()));

        foreach (var (spec, description, value) in NvmlSensor.EnumerateAll())
            result.Add(new(spec, NvmlKind(spec), description, value));
        foreach (var (spec, description, value) in DrmSensor.EnumerateAll())
            result.Add(new(spec, spec.EndsWith(":busy", StringComparison.Ordinal) ? SensorKind.Usage : SensorKind.Clock, description, value));
        return result;
    }

    /// <summary>The kind a field needs (field names as in <see cref="LinuxFrameSource.Fields"/>).</summary>
    public static SensorKind KindOf(string field) => field switch
    {
        LinuxFrameSource.Fields.CpuTemperature or LinuxFrameSource.Fields.GpuTemperature => SensorKind.Temperature,
        LinuxFrameSource.Fields.CpuPower or LinuxFrameSource.Fields.GpuPower => SensorKind.Power,
        LinuxFrameSource.Fields.CpuVoltage => SensorKind.Voltage,
        LinuxFrameSource.Fields.GpuUsage => SensorKind.Usage,
        LinuxFrameSource.Fields.GpuClock => SensorKind.Clock,
        LinuxFrameSource.Fields.Fan or LinuxFrameSource.Fields.Pump => SensorKind.Fan,
        _ => SensorKind.Other,
    };

    /// <summary>Lower-case name used on D-Bus (<c>temperature</c>, <c>power</c>, …).</summary>
    public static string Name(SensorKind kind) => kind.ToString().ToLower(CultureInfo.InvariantCulture);

    private static SensorKind HwmonKind(string kind) => kind switch
    {
        "temp" => SensorKind.Temperature,
        "power" => SensorKind.Power,
        "in" => SensorKind.Voltage,
        "curr" => SensorKind.Current,
        "freq" => SensorKind.Clock,
        "fan" => SensorKind.Fan,
        _ => SensorKind.Other,
    };

    private static SensorKind NvmlKind(string spec) => spec[(spec.IndexOf(':', StringComparison.Ordinal) + 1)..] switch
    {
        "temperature" => SensorKind.Temperature,
        "power" => SensorKind.Power,
        "clock" or "memclock" => SensorKind.Clock,
        "usage" => SensorKind.Usage,
        "fan" => SensorKind.Fan,
        _ => SensorKind.Other,
    };
}
