using System.Globalization;

namespace SegLcd.Linux;

/// <summary>A readable value source (hwmon input, sum of inputs, RAPL meter …).</summary>
public interface ISensor
{
    string Description { get; }

    double? Read();
}

/// <summary>One hwmon value, e.g. <c>hwmon4/temp1</c> = zenpower Tdie.</summary>
public sealed record HwmonSensor(string Id, string Chip, string Label, string Kind, string InputPath) : ISensor
{
    public string Description => $"{Id} ({Chip}:{Label})";

    /// <summary>Raw value converted to the natural unit (°C, RPM, V, W, MHz).</summary>
    public double? Read()
    {
        if (!long.TryParse(LinuxSensors.ReadText(InputPath), NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw))
            return null;
        return Kind switch
        {
            "temp" => raw / 1000.0,      // m°C
            "in" => raw / 1000.0,        // mV
            "power" => raw / 1_000_000.0, // µW
            "freq" => raw / 1_000_000.0,  // Hz -> MHz
            "curr" => raw / 1000.0,      // mA
            _ => raw,                    // fan: RPM
        };
    }
}

/// <summary>Sum of several sensors, e.g. <c>zenpower:SVI2_P_Core+zenpower:SVI2_P_SoC</c>. Null if any part is null.</summary>
public sealed class SumSensor(IReadOnlyList<ISensor> parts) : ISensor
{
    public string Description => string.Join(" + ", parts.Select(p => p.Description));

    public double? Read()
    {
        double sum = 0;
        foreach (var p in parts)
        {
            if (p.Read() is not { } v)
                return null;
            sum += v;
        }

        return sum;
    }
}

/// <summary>
/// Power from a RAPL energy counter (<c>/sys/class/powercap/intel-rapl:*/energy_uj</c>; Intel and AMD Zen).
/// Domains: <c>package</c> (default), <c>core</c>, <c>uncore</c> (Intel iGPU), <c>dram</c>, <c>psys</c>.
/// The counter is root-only since Linux 5.10 (see <c>udev/72-rapl-energy-read.rules</c>).
/// </summary>
public sealed class RaplPowerMeter : ISensor
{
    private const string PowercapRoot = "/sys/class/powercap";
    private readonly string _domainDir;
    private readonly long _maxRange;
    private (long Energy, long Ticks)? _last;

    private RaplPowerMeter(string domainDir, long maxRange)
    {
        _domainDir = domainDir;
        _maxRange = maxRange;
    }

    public string Description => $"RAPL {LinuxSensors.ReadText(Path.Combine(_domainDir, "name"))} ({_domainDir})";

    /// <summary>All RAPL domains (directory, name), e.g. <c>(/sys/class/powercap/intel-rapl:0, package-0)</c>.</summary>
    public static IEnumerable<(string Dir, string Name)> EnumerateDomains()
    {
        if (!Directory.Exists(PowercapRoot))
            yield break;
        foreach (var dir in Directory.EnumerateDirectories(PowercapRoot, "intel-rapl:*").Order(StringComparer.Ordinal))
        {
            if (LinuxSensors.ReadText(Path.Combine(dir, "name")) is { } name)
                yield return (dir, name);
        }
    }

    /// <summary>
    /// Opens the domain whose name starts with <paramref name="domain"/> (first package if <c>null</c>).
    /// Returns <c>null</c> if it does not exist or <c>energy_uj</c> is not readable.
    /// </summary>
    public static RaplPowerMeter? TryCreate(string? domain = null)
    {
        domain = string.IsNullOrWhiteSpace(domain) ? "package" : domain.Trim();
        foreach (var (dir, name) in EnumerateDomains())
        {
            if (!name.StartsWith(domain, StringComparison.OrdinalIgnoreCase) || ReadEnergy(dir) is null)
                continue;
            var range = long.TryParse(LinuxSensors.ReadText(Path.Combine(dir, "max_energy_range_uj")), CultureInfo.InvariantCulture, out var r) ? r : long.MaxValue;
            var meter = new RaplPowerMeter(dir, range);
            meter.Read();
            return meter;
        }

        return null;
    }

    public double? Read()
    {
        if (ReadEnergy(_domainDir) is not { } energy)
            return null;
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var last = _last;
        _last = (energy, now);
        if (last is null)
            return null;
        var delta = energy - last.Value.Energy;
        if (delta < 0)
            delta += _maxRange; // counter wrapped
        var seconds = System.Diagnostics.Stopwatch.GetElapsedTime(last.Value.Ticks, now).TotalSeconds;
        return seconds > 0 ? delta / 1_000_000.0 / seconds : null;
    }

    private static long? ReadEnergy(string dir) =>
        long.TryParse(LinuxSensors.ReadText(Path.Combine(dir, "energy_uj")), CultureInfo.InvariantCulture, out var e) ? e : null;
}

/// <summary>How the per-core clocks are combined into one value.</summary>
public enum CpuClockMode
{
    Avg,
    Max,
    Min,
    First,
}

/// <summary>
/// Reads sensor values from sysfs/procfs. There is no maintained, AOT-compatible .NET library
/// for Linux hwmon, and the interface is just text files, so this is done by hand.
/// </summary>
public static class LinuxSensors
{
    private const string HwmonRoot = "/sys/class/hwmon";

    public static string? ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path).Trim();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static IReadOnlyList<HwmonSensor> EnumerateHwmon()
    {
        var result = new List<HwmonSensor>();
        if (!Directory.Exists(HwmonRoot))
            return result;

        foreach (var dir in Directory.EnumerateDirectories(HwmonRoot).Order(StringComparer.Ordinal))
        {
            var hwmon = Path.GetFileName(dir);
            var chip = ReadText(Path.Combine(dir, "name")) ?? hwmon;
            foreach (var input in Directory.EnumerateFiles(dir, "*_input").Order(StringComparer.Ordinal))
            {
                var file = Path.GetFileName(input);
                var baseName = file[..^"_input".Length]; // temp1, fan2, power1 …
                var kind = new string(baseName.TakeWhile(char.IsLetter).ToArray());
                var label = ReadText(Path.Combine(dir, baseName + "_label")) ?? baseName;
                result.Add(new HwmonSensor($"{hwmon}/{baseName}", chip, label, kind, input));
            }

            // amdgpu exposes power as power1_average on some kernels
            foreach (var avg in Directory.EnumerateFiles(dir, "power*_average"))
            {
                var baseName = Path.GetFileName(avg)[..^"_average".Length];
                if (result.Any(r => r.Id == $"{hwmon}/{baseName}"))
                    continue;
                var label = ReadText(Path.Combine(dir, baseName + "_label")) ?? baseName;
                result.Add(new HwmonSensor($"{hwmon}/{baseName}", chip, label, "power", avg));
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves a sensor spec: <c>hwmon4/temp1</c>, <c>chip:label</c> (<c>zenpower:Tdie</c>),
    /// a sum <c>a+b</c>, <c>rapl[:domain]</c> (power from RAPL), <c>nvml[N]:metric</c> (NVIDIA)
    /// or <c>drm[N]:metric</c> (DRM sysfs: <c>busy</c>, <c>freq</c>).
    /// </summary>
    public static ISensor? Resolve(IReadOnlyList<HwmonSensor> sensors, string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
            return null;
        spec = spec.Trim();
        if (spec.Equals("rapl", StringComparison.OrdinalIgnoreCase))
            return RaplPowerMeter.TryCreate();
        if (spec.StartsWith("rapl:", StringComparison.OrdinalIgnoreCase))
            return RaplPowerMeter.TryCreate(spec[5..]);
        if (NvmlSensor.TryParse(spec) is { } nvml)
            return nvml;
        if (DrmSensor.TryParse(spec) is { } drm)
            return drm;
        if (spec.Contains('+'))
        {
            var parts = spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => Resolve(sensors, p))
                .ToList();
            return parts.All(p => p is not null) ? new SumSensor(parts!) : null;
        }

        return Find(sensors, spec);
    }

    /// <summary>First spec of <paramref name="candidates"/> that resolves.</summary>
    public static ISensor? ResolveFirst(IReadOnlyList<HwmonSensor> sensors, params string[] candidates) =>
        candidates.Select(c => Resolve(sensors, c)).FirstOrDefault(s => s is not null);

    /// <summary>Finds a sensor by id (<c>hwmon4/temp1</c>) or by <c>chip:label</c> (<c>k10temp:Tctl</c>).</summary>
    public static HwmonSensor? Find(IReadOnlyList<HwmonSensor> sensors, string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
            return null;
        var byId = sensors.FirstOrDefault(s => s.Id.Equals(spec, StringComparison.OrdinalIgnoreCase));
        if (byId is not null)
            return byId;
        var parts = spec.Split(':', 2);
        return parts.Length == 2
            ? sensors.FirstOrDefault(s => s.Chip.Equals(parts[0], StringComparison.OrdinalIgnoreCase)
                                          && s.Label.Equals(parts[1], StringComparison.OrdinalIgnoreCase))
            : null;
    }


    /// <summary>CPU usage from two samples of <c>/proc/stat</c>.</summary>
    public sealed class CpuUsageMeter
    {
        private (ulong Idle, ulong Total)? _last;

        public double? Sample()
        {
            var line = File.ReadLines("/proc/stat").FirstOrDefault(l => l.StartsWith("cpu ", StringComparison.Ordinal));
            if (line is null)
                return null;
            var values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)
                .Select(v => ulong.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            var idle = values[3] + (values.Length > 4 ? values[4] : 0); // idle + iowait
            var total = values.Aggregate(0UL, (a, b) => a + b);
            var last = _last;
            _last = (idle, total);
            if (last is null || total == last.Value.Total)
                return null;
            return 100.0 * (1.0 - (double)(idle - last.Value.Idle) / (total - last.Value.Total));
        }
    }

    /// <summary>RAM usage in percent from <c>/proc/meminfo</c> (MemTotal − MemAvailable).</summary>
    public static double? RamUsage()
    {
        long? total = null, available = null;
        foreach (var line in File.ReadLines("/proc/meminfo"))
        {
            var parts = line.Split(':', 2);
            if (parts.Length != 2)
                continue;
            var kb = long.Parse(parts[1].Trim().Split(' ')[0], CultureInfo.InvariantCulture);
            if (parts[0] == "MemTotal") total = kb;
            else if (parts[0] == "MemAvailable") available = kb;
        }

        return total is > 0 && available is not null ? 100.0 * (total.Value - available.Value) / total.Value : null;
    }

    /// <summary>Current CPU clock in MHz from cpufreq (<c>scaling_cur_freq</c> of all cores), aggregated by <paramref name="mode"/>.</summary>
    public static double? CpuClock(CpuClockMode mode = CpuClockMode.Avg)
    {
        var khz = Directory.EnumerateDirectories("/sys/devices/system/cpu", "cpu*")
            .Where(d => char.IsDigit(Path.GetFileName(d)[^1]))
            .OrderBy(d => int.Parse(Path.GetFileName(d)[3..], CultureInfo.InvariantCulture))
            .Select(d => ReadText(Path.Combine(d, "cpufreq", "scaling_cur_freq")))
            .Select(t => long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0)
            .Where(v => v > 0)
            .ToList();
        if (khz.Count == 0)
            return null;
        return mode switch
        {
            CpuClockMode.Max => khz.Max(),
            CpuClockMode.Min => khz.Min(),
            CpuClockMode.First => khz[0],
            _ => khz.Average(),
        } / 1000.0;
    }

    /// <summary>GPU usage of the first DRM card that exposes <c>gpu_busy_percent</c> (amdgpu, i915/xe partly).</summary>
    public static double? GpuUsage()
    {
        if (!Directory.Exists("/sys/class/drm"))
            return null;
        foreach (var card in Directory.EnumerateDirectories("/sys/class/drm", "card*").Order(StringComparer.Ordinal))
        {
            var text = ReadText(Path.Combine(card, "device", "gpu_busy_percent"));
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
        }

        return null;
    }
}