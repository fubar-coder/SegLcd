using System.Text.Json.Serialization;

using SegLcd.Linux;
using SegLcd.Localization;

using Tomlyn;
using Tomlyn.Serialization;

namespace SegLcd.Daemon;

/// <summary>Contents of <c>config.toml</c>. Keys are snake_case (e.g. <c>update_interval_ms</c>).</summary>
public sealed class DaemonConfig
{
    public const int MinUpdateIntervalMs = 50;
    public const int MinSampleIntervalMs = 100;
    public const int MaxSampleIntervalMs = 10_000;

    /// <summary>Highest interval that keeps the LB360-S on (timeout ≈ 400–500 ms).</summary>
    public static int MaxUpdateIntervalMs => (int)SegmentLcdProtocol.MaxReliableUpdateInterval.TotalMilliseconds;

    /// <summary>Feed the display (<c>false</c> = off, device released).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Packet interval in ms (50 – 400).</summary>
    public int UpdateIntervalMs { get; set; } = 250;

    /// <summary>Sensor sampling interval in ms (100 – 10000).</summary>
    public int SampleIntervalMs { get; set; } = 1000;

    /// <summary>Optional device path (e.g. <c>/dev/hidraw5</c>); empty = first segment LCD found.</summary>
    public string? Device { get; set; }

    public SensorSection Sensors { get; set; } = new();

    /// <summary>Checks ranges; throws <see cref="ArgumentException"/> with a readable message.</summary>
    public void Validate()
    {
        if (UpdateIntervalMs < MinUpdateIntervalMs || UpdateIntervalMs > MaxUpdateIntervalMs)
            throw new ArgumentException(Texts.Current.T("update_interval_ms must be between {0} and {1} (is {2}).", MinUpdateIntervalMs, MaxUpdateIntervalMs, UpdateIntervalMs));
        if (SampleIntervalMs < MinSampleIntervalMs || SampleIntervalMs > MaxSampleIntervalMs)
            throw new ArgumentException(Texts.Current.T("sample_interval_ms must be between {0} and {1} (is {2}).", MinSampleIntervalMs, MaxSampleIntervalMs, SampleIntervalMs));
        _ = Sensors.ToSettings();
    }
}

/// <summary><c>[sensors]</c> table. Empty or missing values select the automatic default.</summary>
public sealed class SensorSection
{
    public string? CpuTemperature { get; set; }

    public string? CpuPower { get; set; }

    public string? CpuVoltage { get; set; }

    public string? GpuTemperature { get; set; }

    public string? GpuUsage { get; set; }

    public string? GpuPower { get; set; }

    public string? GpuClock { get; set; }

    public string? Fan { get; set; }

    public string? Pump { get; set; }

    /// <summary><c>avg</c>, <c>max</c>, <c>min</c> or <c>first</c>.</summary>
    public string CpuClockMode { get; set; } = "avg";

    public SensorSettings ToSettings() => new()
    {
        CpuTemperature = Empty(CpuTemperature),
        CpuPower = Empty(CpuPower),
        CpuVoltage = Empty(CpuVoltage),
        GpuTemperature = Empty(GpuTemperature),
        GpuUsage = Empty(GpuUsage),
        GpuPower = Empty(GpuPower),
        GpuClock = Empty(GpuClock),
        Fan = Empty(Fan),
        Pump = Empty(Pump),
        CpuClockMode = Enum.TryParse<CpuClockMode>(CpuClockMode, ignoreCase: true, out var mode)
            ? mode
            : throw new ArgumentException(Texts.Current.T("sensors.cpu_clock_mode '{0}' is invalid (avg, max, min, first).", CpuClockMode)),
    };

    public static SensorSection From(SensorSettings s) => new()
    {
        CpuTemperature = s.CpuTemperature,
        CpuPower = s.CpuPower,
        CpuVoltage = s.CpuVoltage,
        GpuTemperature = s.GpuTemperature,
        GpuUsage = s.GpuUsage,
        GpuPower = s.GpuPower,
        GpuClock = s.GpuClock,
        Fan = s.Fan,
        Pump = s.Pump,
        CpuClockMode = s.CpuClockMode.ToString().ToLowerInvariant(),
    };

    private static string? Empty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

[TomlSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[TomlSerializable(typeof(DaemonConfig))]
internal sealed partial class ConfigTomlContext : TomlSerializerContext;

/// <summary>How seglcdd runs: as systemd user service (session bus) or system service (system bus).</summary>
public enum DaemonMode
{
    User,
    System,
}

/// <summary>Run-time mode, registered in DI.</summary>
public sealed record DaemonOptions(DaemonMode Mode);

/// <summary>Loads and saves <see cref="DaemonConfig"/> as TOML.</summary>
public sealed class ConfigStore(string path)
{
    /// <summary><c>$XDG_CONFIG_HOME/seglcd/config.toml</c>, falling back to <c>~/.config/seglcd/config.toml</c>.</summary>
    public static string DefaultPath
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var root = string.IsNullOrEmpty(xdg)
                ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
                : xdg;
            return System.IO.Path.Combine(root, "seglcd", "config.toml");
        }
    }

    /// <summary>Path of the system-wide configuration used with <c>--system</c>.</summary>
    public const string SystemPath = "/etc/seglcd/config.toml";

    /// <summary>Default configuration path for <paramref name="mode"/>.</summary>
    public static string DefaultPathFor(DaemonMode mode) => mode == DaemonMode.System ? SystemPath : DefaultPath;

    public string Path { get; } = path;

    /// <summary>Reads the file; a missing file yields the defaults.</summary>
    /// <exception cref="InvalidDataException">Syntax or value error, message includes the file name.</exception>
    public DaemonConfig Load()
    {
        if (!File.Exists(Path))
            return new DaemonConfig();
        try
        {
            var config = TomlSerializer.Deserialize(File.ReadAllText(Path), ConfigTomlContext.Default.DaemonConfig) ?? new DaemonConfig();
            config.Sensors ??= new SensorSection();
            config.Validate();
            return config;
        }
        catch (Exception e) when (e is not IOException)
        {
            throw new InvalidDataException($"{Path}: {e.Message}", e);
        }
    }

    /// <summary>Writes the configuration (atomically via a temporary file).</summary>
    public void Save(DaemonConfig config)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        // Machine-written file: comments stay English (see packaging/config.example.toml for the documented version).
        var toml = "# seglcdd configuration (written by SaveConfig). Empty sensor values = automatic selection.\n"
                   + "# Sensor specs: chip:label, hwmonN/name, a+b, rapl[:domain], nvml[N]:metric, drm[N]:metric. See 'seglcd sensors'.\n\n"
                   + TomlSerializer.Serialize(config, ConfigTomlContext.Default.DaemonConfig);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, toml);
        File.Move(tmp, Path, overwrite: true);
    }
}