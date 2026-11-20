using System.Globalization;
using System.Runtime.InteropServices;

namespace SegLcd.Linux;

/// <summary>
/// NVIDIA GPU values via NVML (<c>libnvidia-ml.so.1</c>, part of the proprietary and the open NVIDIA driver).
/// Spec: <c>nvml:metric</c> or <c>nvmlN:metric</c> (GPU index N, default 0);
/// metrics: <c>temperature</c> (°C), <c>power</c> (W), <c>clock</c> (graphics MHz), <c>memclock</c> (MHz),
/// <c>usage</c> (%), <c>fan</c> (% of max – NVML reports no RPM).
/// </summary>
/// <remarks>The library is loaded on first use; without NVIDIA driver every spec simply does not resolve.</remarks>
public sealed class NvmlSensor : ISensor
{
    private static readonly string[] Metrics = ["temperature", "power", "clock", "memclock", "usage", "fan"];
    private readonly uint _index;
    private readonly string _metric;

    private NvmlSensor(uint index, string metric)
    {
        _index = index;
        _metric = metric;
    }

    public string Description => $"NVML GPU {_index} {Nvml.GetName(_index) ?? "?"} {_metric}";

    /// <summary>Parses <c>nvml[N]:metric</c>; returns <c>null</c> if the spec is not NVML or the GPU is not available.</summary>
    public static NvmlSensor? TryParse(string spec)
    {
        var colon = spec.IndexOf(':');
        if (colon < 4 || !spec.StartsWith("nvml", StringComparison.OrdinalIgnoreCase))
            return null;
        var indexText = spec[4..colon];
        uint index = 0;
        if (indexText.Length > 0 && !uint.TryParse(indexText, CultureInfo.InvariantCulture, out index))
            return null;
        var metric = spec[(colon + 1)..].Trim().ToLowerInvariant();
        if (!Metrics.Contains(metric) || index >= Nvml.DeviceCount)
            return null;
        var sensor = new NvmlSensor(index, metric);
        return sensor.Read() is null ? null : sensor;
    }

    /// <summary>All NVML GPUs with their current values (for <c>seglcd sensors</c>).</summary>
    public static IEnumerable<(string Spec, string Description, double? Value)> EnumerateAll()
    {
        for (uint i = 0; i < Nvml.DeviceCount; i++)
        {
            foreach (var m in Metrics)
            {
                var s = new NvmlSensor(i, m);
                yield return ($"nvml{(i == 0 ? string.Empty : i.ToString(CultureInfo.InvariantCulture))}:{m}", s.Description, s.Read());
            }
        }
    }

    public double? Read() => _metric switch
    {
        "temperature" => Nvml.Temperature(_index),
        "power" => Nvml.PowerMilliwatts(_index) / 1000.0,
        "clock" => Nvml.Clock(_index, Nvml.ClockGraphics),
        "memclock" => Nvml.Clock(_index, Nvml.ClockMemory),
        "usage" => Nvml.GpuUtilization(_index),
        "fan" => Nvml.FanPercent(_index),
        _ => null,
    };
}

/// <summary>Minimal NVML binding loaded with <see cref="NativeLibrary"/> (AOT-safe, no static import).</summary>
internal static unsafe class Nvml
{
    public const int ClockGraphics = 0;
    public const int ClockMemory = 2;
    private const int Success = 0;
    private const int TemperatureGpu = 0;

    private static readonly Lock InitLock = new();
    private static bool _initialized;
    private static bool _available;
    private static uint _count;

    private static delegate* unmanaged<uint*, int> _getCount;
    private static delegate* unmanaged<uint, nint*, int> _getHandle;
    private static delegate* unmanaged<nint, int, uint*, int> _getTemperature;
    private static delegate* unmanaged<nint, uint*, int> _getPower;
    private static delegate* unmanaged<nint, int, uint*, int> _getClock;
    private static delegate* unmanaged<nint, uint*, int> _getUtilization; // nvmlUtilization_t { uint gpu; uint memory; }
    private static delegate* unmanaged<nint, uint*, int> _getFan;
    private static delegate* unmanaged<nint, byte*, uint, int> _getName;

    public static uint DeviceCount => EnsureInit() ? _count : 0;

    public static double? Temperature(uint index) => Query(index, (h, v) => _getTemperature(h, TemperatureGpu, (uint*)v));

    public static double? PowerMilliwatts(uint index) => Query(index, (h, v) => _getPower(h, (uint*)v));

    public static double? Clock(uint index, int type) => Query(index, (h, v) => _getClock(h, type, (uint*)v));

    public static double? FanPercent(uint index) => _getFan == null ? null : Query(index, (h, v) => _getFan(h, (uint*)v));

    public static double? GpuUtilization(uint index)
    {
        if (!TryHandle(index, out var h))
            return null;
        var rates = stackalloc uint[2];
        return _getUtilization(h, rates) == Success ? rates[0] : null;
    }

    public static string? GetName(uint index)
    {
        if (_getName == null || !TryHandle(index, out var h))
            return null;
        var buffer = stackalloc byte[96];
        return _getName(h, buffer, 96) == Success ? Marshal.PtrToStringUTF8((nint)buffer) : null;
    }

    private static double? Query(uint index, Func<nint, nint, int> call)
    {
        if (!TryHandle(index, out var h))
            return null;
        uint value;
        return call(h, (nint)(&value)) == Success ? value : null;
    }

    private static bool TryHandle(uint index, out nint handle)
    {
        handle = 0;
        if (!EnsureInit() || index >= _count)
            return false;
        nint h;
        if (_getHandle(index, &h) != Success)
            return false;
        handle = h;
        return true;
    }

    private static bool EnsureInit()
    {
        lock (InitLock)
        {
            if (_initialized)
                return _available;
            _initialized = true;
            if (!NativeLibrary.TryLoad("libnvidia-ml.so.1", out var lib))
                return false;
            if (!NativeLibrary.TryGetExport(lib, "nvmlInit_v2", out var init)
                || !NativeLibrary.TryGetExport(lib, "nvmlDeviceGetCount_v2", out var count)
                || !NativeLibrary.TryGetExport(lib, "nvmlDeviceGetHandleByIndex_v2", out var handle)
                || !NativeLibrary.TryGetExport(lib, "nvmlDeviceGetTemperature", out var temp)
                || !NativeLibrary.TryGetExport(lib, "nvmlDeviceGetPowerUsage", out var power)
                || !NativeLibrary.TryGetExport(lib, "nvmlDeviceGetClockInfo", out var clock)
                || !NativeLibrary.TryGetExport(lib, "nvmlDeviceGetUtilizationRates", out var util))
                return false;
            _getCount = (delegate* unmanaged<uint*, int>)count;
            _getHandle = (delegate* unmanaged<uint, nint*, int>)handle;
            _getTemperature = (delegate* unmanaged<nint, int, uint*, int>)temp;
            _getPower = (delegate* unmanaged<nint, uint*, int>)power;
            _getClock = (delegate* unmanaged<nint, int, uint*, int>)clock;
            _getUtilization = (delegate* unmanaged<nint, uint*, int>)util;
            if (NativeLibrary.TryGetExport(lib, "nvmlDeviceGetFanSpeed", out var fan))
                _getFan = (delegate* unmanaged<nint, uint*, int>)fan;
            if (NativeLibrary.TryGetExport(lib, "nvmlDeviceGetName", out var name))
                _getName = (delegate* unmanaged<nint, byte*, uint, int>)name;

            if (((delegate* unmanaged<int>)init)() != Success)
                return false;
            uint n;
            if (_getCount(&n) != Success)
                return false;
            _count = n;
            _available = true;
            return true;
        }
    }
}

/// <summary>
/// GPU values from DRM sysfs. Spec: <c>drm:metric</c> or <c>drmN:metric</c> (card N; default: first card providing it).
/// Metrics: <c>busy</c> (%, <c>device/gpu_busy_percent</c> – amdgpu),
/// <c>freq</c> (MHz, actual GPU frequency – Intel i915 <c>gt_act_freq_mhz</c> or xe <c>device/tile0/gt0/freq0/act_freq</c>).
/// </summary>
public sealed class DrmSensor : ISensor
{
    private const string DrmRoot = "/sys/class/drm";
    private readonly string _path;
    private readonly string _metric;
    private readonly string _card;

    private DrmSensor(string card, string metric, string path)
    {
        _card = card;
        _metric = metric;
        _path = path;
    }

    public string Description => $"DRM {_card} {_metric} ({_path})";

    public static DrmSensor? TryParse(string spec)
    {
        var colon = spec.IndexOf(':');
        if (colon < 3 || !spec.StartsWith("drm", StringComparison.OrdinalIgnoreCase))
            return null;
        var cardText = spec[3..colon];
        if (cardText.Length > 0 && !cardText.All(char.IsDigit))
            return null;
        var metric = spec[(colon + 1)..].Trim().ToLowerInvariant();
        var cards = cardText.Length > 0 ? [Path.Combine(DrmRoot, "card" + cardText)] : EnumerateCards();
        foreach (var card in cards)
        {
            foreach (var candidate in Candidates(card, metric))
            {
                if (File.Exists(candidate) && ParseValue(LinuxSensors.ReadText(candidate)) is not null)
                    return new DrmSensor(Path.GetFileName(card), metric, candidate);
            }
        }

        return null;
    }

    /// <summary>All DRM values that exist on this system (for <c>seglcd sensors</c>).</summary>
    public static IEnumerable<(string Spec, string Description, double? Value)> EnumerateAll()
    {
        foreach (var card in EnumerateCards())
        {
            var n = Path.GetFileName(card)[4..];
            foreach (var metric in new[] { "busy", "freq" })
            {
                if (TryParse($"drm{n}:{metric}") is { } s)
                    yield return ($"drm{n}:{metric}", s.Description, s.Read());
            }
        }
    }

    public double? Read() => ParseValue(LinuxSensors.ReadText(_path));

    private static IEnumerable<string> Candidates(string card, string metric) => metric switch
    {
        "busy" => [Path.Combine(card, "device", "gpu_busy_percent")],
        "freq" =>
        [
            Path.Combine(card, "gt_act_freq_mhz"),                              // i915
            Path.Combine(card, "device", "tile0", "gt0", "freq0", "act_freq"), // xe
            Path.Combine(card, "gt_cur_freq_mhz"),                              // i915 (requested)
        ],
        _ => [],
    };

    private static string[] EnumerateCards() =>
        Directory.Exists(DrmRoot)
            ? Directory.EnumerateDirectories(DrmRoot, "card*")
                .Where(d => Path.GetFileName(d)[4..].All(char.IsDigit)) // skip connectors like card0-DP-1
                .Order(StringComparer.Ordinal)
                .ToArray()
            : [];

    private static double? ParseValue(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
}