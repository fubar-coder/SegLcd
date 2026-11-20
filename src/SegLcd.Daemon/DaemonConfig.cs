using System.Text.Json.Serialization;

using SegLcd.Linux;
using SegLcd.Localization;

using Tomlyn;
using Tomlyn.Model;
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
[TomlSerializable(typeof(TomlTable))]
internal sealed partial class ConfigTomlContext : TomlSerializerContext;

/// <summary>How seglcdd runs: as systemd user service (session bus) or system service (system bus).</summary>
public enum DaemonMode
{
    User,
    System,
}

/// <summary>Run-time mode, registered in DI.</summary>
public sealed record DaemonOptions(DaemonMode Mode);

/// <summary>
/// Loads and saves <see cref="DaemonConfig"/> as TOML. With a <see cref="StatePath"/> (system service) the file at
/// <see cref="Path"/> is only read and <see cref="Save"/> writes the values that differ from it to
/// <see cref="StatePath"/>; on loading, those override <see cref="Path"/>.
/// </summary>
public sealed class ConfigStore(string path, string? statePath = null)
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

    /// <summary>
    /// Settings saved via D-Bus with <c>--system</c>: <c>$STATE_DIRECTORY/config.toml</c> (systemd
    /// <c>StateDirectory=seglcd</c>), falling back to <c>/var/lib/seglcd/config.toml</c>.
    /// </summary>
    public static string SystemStatePath(Func<string, string?> getEnv)
    {
        var dir = getEnv("STATE_DIRECTORY")?.Split(':')[0];
        return System.IO.Path.Combine(string.IsNullOrEmpty(dir) ? "/var/lib/seglcd" : dir, "config.toml");
    }

    /// <summary>Default configuration path for <paramref name="mode"/>.</summary>
    public static string DefaultPathFor(DaemonMode mode) => mode == DaemonMode.System ? SystemPath : DefaultPath;

    /// <summary>Configuration file (read; written by <see cref="Save"/> only without <see cref="StatePath"/>).</summary>
    public string Path { get; } = path;

    /// <summary>Writable file with saved overrides, or <c>null</c>.</summary>
    public string? StatePath { get; } = statePath;

    /// <summary>File written by <see cref="Save"/>.</summary>
    public string SavePath => StatePath ?? Path;

    /// <summary>Reads the configuration and the saved overrides; missing files yield the defaults.</summary>
    /// <exception cref="InvalidDataException">Syntax or value error, message includes the file name.</exception>
    public DaemonConfig Load()
    {
        var table = ReadTable(Path) ?? new TomlTable();
        var files = Path;
        if (StatePath is not null && ReadTable(StatePath) is { } state)
        {
            Merge(table, state);
            files = $"{Path} + {StatePath}";
        }

        return ToConfig(table, files);
    }

    /// <summary>
    /// Writes the configuration atomically. With <see cref="StatePath"/> only the values that differ from
    /// <see cref="Path"/> are written; without differences the file is removed.
    /// </summary>
    public void Save(DaemonConfig config)
    {
        if (StatePath is null)
        {
            // Machine-written file: comments stay English (see packaging/config.example.toml for the documented version).
            Write(Path, "# seglcdd configuration (written by SaveConfig). Empty sensor values = automatic selection.\n"
                        + "# Sensor specs: chip:label, hwmonN/name, a+b, rapl[:domain], nvml[N]:metric, drm[N]:metric. See 'seglcd sensors'.\n\n"
                        + TomlSerializer.Serialize(config, ConfigTomlContext.Default.DaemonConfig));
            return;
        }

        DaemonConfig baseConfig;
        try
        {
            baseConfig = ToConfig(ReadTable(Path) ?? new TomlTable(), Path);
        }
        catch (InvalidDataException)
        {
            baseConfig = new DaemonConfig();
        }

        var diff = Diff(ToTable(baseConfig), ToTable(config));
        if (diff.Count == 0)
        {
            File.Delete(StatePath);
            return;
        }

        Write(StatePath, $"# Settings saved by seglcdd (D-Bus SaveConfig). They override {Path}.\n"
                         + $"# Delete this file to use {Path} only. Empty sensor values = automatic selection.\n\n"
                         + TomlSerializer.Serialize(diff, ConfigTomlContext.Default.TomlTable));
    }

    private static TomlTable? ReadTable(string file)
    {
        if (!File.Exists(file))
            return null;
        try
        {
            return TomlSerializer.Deserialize(File.ReadAllText(file), ConfigTomlContext.Default.TomlTable) ?? new TomlTable();
        }
        catch (Exception e) when (e is not IOException)
        {
            throw new InvalidDataException($"{file}: {e.Message}", e);
        }
    }

    private static DaemonConfig ToConfig(TomlTable table, string files)
    {
        try
        {
            var toml = TomlSerializer.Serialize(table, ConfigTomlContext.Default.TomlTable);
            var config = TomlSerializer.Deserialize(toml, ConfigTomlContext.Default.DaemonConfig) ?? new DaemonConfig();
            config.Sensors ??= new SensorSection();
            if (string.IsNullOrWhiteSpace(config.Device))
                config.Device = null;
            config.Validate();
            return config;
        }
        catch (Exception e) when (e is not IOException)
        {
            throw new InvalidDataException($"{files}: {e.Message}", e);
        }
    }

    private static TomlTable ToTable(DaemonConfig config) =>
        TomlSerializer.Deserialize(TomlSerializer.Serialize(config, ConfigTomlContext.Default.DaemonConfig),
            ConfigTomlContext.Default.TomlTable) ?? new TomlTable();

    /// <summary>Copies the values of <paramref name="overrides"/> into <paramref name="target"/>, tables recursively.</summary>
    private static void Merge(TomlTable target, TomlTable overrides)
    {
        foreach (var (key, value) in overrides)
        {
            if (value is TomlTable sub && target.TryGetValue(key, out var existing) && existing is TomlTable targetSub)
                Merge(targetSub, sub);
            else
                target[key] = value;
        }
    }

    /// <summary>
    /// Values of <paramref name="current"/> that differ from <paramref name="baseline"/>. A string set in the
    /// baseline but unset now (automatic sensor, any device) becomes <c>""</c>.
    /// </summary>
    private static TomlTable Diff(TomlTable baseline, TomlTable current)
    {
        var diff = new TomlTable();
        foreach (var (key, value) in current)
        {
            baseline.TryGetValue(key, out var old);
            if (value is TomlTable sub)
            {
                var subDiff = Diff(old as TomlTable ?? new TomlTable(), sub);
                if (subDiff.Count > 0)
                    diff[key] = subDiff;
            }
            else if (!Equals(value, old))
            {
                diff[key] = value;
            }
        }

        foreach (var (key, old) in baseline)
        {
            if (!current.ContainsKey(key) && old is string { Length: > 0 })
                diff[key] = string.Empty;
        }

        return diff;
    }

    private static void Write(string file, string content)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, file, overwrite: true);
    }
}
