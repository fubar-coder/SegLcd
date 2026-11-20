using SegLcd.Localization;

namespace SegLcd.Linux;

/// <summary>
/// Which sensor feeds which frame field. <c>null</c> or empty = automatic default
/// (see <see cref="LinuxFrameSource.Defaults"/>). Specs: <c>hwmonN/name</c>, <c>chip:label</c>,
/// <c>a+b</c> (sum) or <c>rapl</c> (CPU package power).
/// </summary>
public sealed record SensorSettings
{
    public string? CpuTemperature { get; init; }

    public string? CpuPower { get; init; }

    public string? CpuVoltage { get; init; }

    public string? GpuTemperature { get; init; }

    public string? GpuUsage { get; init; }

    public string? GpuPower { get; init; }

    public string? GpuClock { get; init; }

    public string? Fan { get; init; }

    public string? Pump { get; init; }

    public CpuClockMode CpuClockMode { get; init; } = CpuClockMode.Avg;
}

/// <summary>Builds <see cref="SegmentLcdFrame"/>s from Linux sensors (hwmon, procfs, cpufreq, RAPL).</summary>
public sealed class LinuxFrameSource
{
    /// <summary>Field names used in configuration files and on D-Bus.</summary>
    public static class Fields
    {
        public const string CpuTemperature = "cpu_temperature";
        public const string CpuPower = "cpu_power";
        public const string CpuVoltage = "cpu_voltage";
        public const string GpuTemperature = "gpu_temperature";
        public const string GpuUsage = "gpu_usage";
        public const string GpuPower = "gpu_power";
        public const string GpuClock = "gpu_clock";
        public const string Fan = "fan";
        public const string Pump = "pump";

        /// <summary>Fields whose source can be configured.</summary>
        public static IReadOnlyList<string> Configurable { get; } =
            [CpuTemperature, CpuPower, CpuVoltage, GpuTemperature, GpuUsage, GpuPower, GpuClock, Fan, Pump];
    }

    /// <summary>Candidates tried in order when a field is not configured. Fan headers are board-specific: no default.</summary>
    public static IReadOnlyDictionary<string, string[]> Defaults { get; } = new Dictionary<string, string[]>
    {
        // CPU: AMD (k10temp/zenpower), Intel (coretemp). RAPL works on both, but is root-only
        // unless udev/72-rapl-energy-read.rules is installed; k10temp has no power sensor.
        [Fields.CpuTemperature] = ["k10temp:Tctl", "zenpower:Tdie", "zenpower:Tctl", "coretemp:Package id 0"],
        [Fields.CpuPower] = ["rapl", "zenpower:SVI2_P_Core+zenpower:SVI2_P_SoC"],
        [Fields.CpuVoltage] = ["zenpower:SVI2_Core"],
        // GPU: AMD (amdgpu hwmon, DRM), NVIDIA (NVML), Intel (xe hwmon, DRM frequency, RAPL uncore for iGPUs).
        // On hybrid systems the first match wins – configure explicitly if needed.
        [Fields.GpuTemperature] = ["amdgpu:edge", "nvml:temperature", "xe:pkg"],
        [Fields.GpuUsage] = ["drm:busy", "nvml:usage"],
        [Fields.GpuPower] = ["amdgpu:PPT", "nvml:power", "rapl:uncore"],
        [Fields.GpuClock] = ["amdgpu:sclk", "nvml:clock", "drm:freq"],
        [Fields.Fan] = [],
        [Fields.Pump] = [],
    };

    private readonly Dictionary<string, ISensor?> _sources;
    private readonly LinuxSensors.CpuUsageMeter _cpuUsage = new();

    public LinuxFrameSource(SensorSettings settings)
    {
        Settings = settings;
        var hwmon = LinuxSensors.EnumerateHwmon();
        _sources = Fields.Configurable.ToDictionary(f => f, f => Resolve(hwmon, f, GetSpec(settings, f)));
        _cpuUsage.Sample();
    }

    public SensorSettings Settings { get; }

    /// <summary>Resolved source per field (description), "–" if none.</summary>
    public IReadOnlyDictionary<string, string> Sources =>
        _sources.ToDictionary(kv => kv.Key, kv => kv.Value?.Description ?? "–");

    /// <summary>Reads all sensors once.</summary>
    public SegmentLcdFrame Sample() => new()
    {
        CpuTemperature = Read(Fields.CpuTemperature),
        CpuUsage = _cpuUsage.Sample(),
        CpuPower = Read(Fields.CpuPower),
        CpuClock = LinuxSensors.CpuClock(Settings.CpuClockMode),
        CpuVoltage = Read(Fields.CpuVoltage),
        GpuTemperature = Read(Fields.GpuTemperature),
        GpuUsage = Read(Fields.GpuUsage),
        GpuPower = Read(Fields.GpuPower),
        GpuClock = Read(Fields.GpuClock),
        FanSpeed = Read(Fields.Fan),
        PumpSpeed = Read(Fields.Pump),
        RamUsage = LinuxSensors.RamUsage(),
    };

    /// <summary>Returns the spec configured for <paramref name="field"/>.</summary>
    public static string? GetSpec(SensorSettings s, string field) => field switch
    {
        Fields.CpuTemperature => s.CpuTemperature,
        Fields.CpuPower => s.CpuPower,
        Fields.CpuVoltage => s.CpuVoltage,
        Fields.GpuTemperature => s.GpuTemperature,
        Fields.GpuUsage => s.GpuUsage,
        Fields.GpuPower => s.GpuPower,
        Fields.GpuClock => s.GpuClock,
        Fields.Fan => s.Fan,
        Fields.Pump => s.Pump,
        _ => throw new ArgumentException(Texts.Current.T("Unknown field '{0}'. Allowed: {1}", field, string.Join(", ", Fields.Configurable))),
    };

    /// <summary>Returns a copy of <paramref name="s"/> with <paramref name="field"/> set to <paramref name="spec"/> (empty = automatic).</summary>
    public static SensorSettings WithSpec(SensorSettings s, string field, string? spec)
    {
        spec = string.IsNullOrWhiteSpace(spec) ? null : spec.Trim();
        return field switch
        {
            Fields.CpuTemperature => s with { CpuTemperature = spec },
            Fields.CpuPower => s with { CpuPower = spec },
            Fields.CpuVoltage => s with { CpuVoltage = spec },
            Fields.GpuTemperature => s with { GpuTemperature = spec },
            Fields.GpuUsage => s with { GpuUsage = spec },
            Fields.GpuPower => s with { GpuPower = spec },
            Fields.GpuClock => s with { GpuClock = spec },
            Fields.Fan => s with { Fan = spec },
            Fields.Pump => s with { Pump = spec },
            _ => throw new ArgumentException(Texts.Current.T("Unknown field '{0}'. Allowed: {1}", field, string.Join(", ", Fields.Configurable))),
        };
    }

    /// <summary>
    /// Checks that <paramref name="spec"/> resolves on this system; returns the description.
    /// </summary>
    /// <exception cref="ArgumentException">Spec does not resolve.</exception>
    public static string Validate(string spec)
    {
        var sensor = LinuxSensors.Resolve(LinuxSensors.EnumerateHwmon(), spec)
                     ?? throw new ArgumentException(Texts.Current.T("Sensor '{0}' not found (see 'seglcd sensors').", spec));
        return sensor.Description;
    }

    private double? Read(string field) => _sources[field]?.Read();

    private static ISensor? Resolve(IReadOnlyList<HwmonSensor> hwmon, string field, string? spec) =>
        string.IsNullOrWhiteSpace(spec)
            ? LinuxSensors.ResolveFirst(hwmon, Defaults[field])
            : LinuxSensors.Resolve(hwmon, spec);
}