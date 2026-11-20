using System.Globalization;

using DotMake.CommandLine;

using HidApi;

using Microsoft.Extensions.Localization;

using SegLcd.Linux;
using SegLcd.Localization;

namespace SegLcd.Tool;

[CliCommand(Description = nameof(CliTexts.ListDescription))]
public sealed class ListCommand
{
    [CliOption(Description = nameof(CliTexts.ListAllOption))]
    public bool All { get; set; }

    public int Run()
    {
        var devices = All
            ? Hid.Enumerate().ToList()
            : SegmentLcdDevice.Enumerate().ToList();

        if (devices.Count == 0)
        {
            Console.WriteLine(L.T("No matching devices found."));
            return 1;
        }

        foreach (var d in devices)
        {
            var known = KnownDevices.Find(d.VendorId, d.ProductId);
            Console.WriteLine(d.Path);
            Row("VID:PID", $"{d.VendorId:x4}:{d.ProductId:x4}  {known?.Name ?? L.T("(unknown)")}"
                           + (known is { ProtocolVerified: false } ? "  " + L.T("[protocol unverified]") : string.Empty));
            Row("Bus", $"{d.BusType}, Interface {d.InterfaceNumber}");
            Row("Usage", $"Page 0x{d.UsagePage:x4}, Usage 0x{d.Usage:x4}");
            Row("Release", $"{d.ReleaseNumber:x4}");
            Row(L.T("Manufacturer"), OrEmpty(d.ManufacturerString));
            Row(L.T("Product"), OrEmpty(d.ProductString));
            Row(L.T("Serial number"), OrEmpty(d.SerialNumber));

            if (known is null)
            {
                Console.WriteLine();
                continue;
            }

            using var device = SegmentLcdDevice.TryOpen(d);
            Row(L.T("Open"), device is null ? L.T("failed (permissions? udev rule)") : "OK");
            if (device is not null)
                Row(L.T("Report descr."), Try(() => Hex.Format(device.GetReportDescriptor(), trimTrailingZeros: false)));
            Console.WriteLine();
        }

        return 0;
    }

    private static IStringLocalizer L => Texts.Current;

    private static void Row(string label, string value) => Console.WriteLine($"  {label,-15}{value}");

    private static string OrEmpty(string? s) => string.IsNullOrEmpty(s) ? L.T("(empty)") : s;

    private static string Try(Func<string> f)
    {
        try
        {
            return f();
        }
        catch (Exception e)
        {
            return $"({e.GetType().Name}: {e.Message})";
        }
    }
}

[CliCommand(Description = nameof(CliTexts.SendDescription))]
public sealed class SendCommand : DeviceCommandBase, ITemperatureUnitOption
{
    [CliOption(Description = nameof(CliTexts.CpuTempOption), Required = false)] public double? CpuTemp { get; set; }
    [CliOption(Description = nameof(CliTexts.CpuUsageOption), Required = false)] public double? CpuUsage { get; set; }
    [CliOption(Description = nameof(CliTexts.CpuPowerOption), Required = false)] public double? CpuPower { get; set; }
    [CliOption(Description = nameof(CliTexts.CpuClockOption), Required = false)] public double? CpuClock { get; set; }
    [CliOption(Description = nameof(CliTexts.CpuVoltageOption), Required = false)] public double? CpuVoltage { get; set; }
    [CliOption(Description = nameof(CliTexts.GpuTempOption), Required = false)] public double? GpuTemp { get; set; }
    [CliOption(Description = nameof(CliTexts.GpuUsageOption), Required = false)] public double? GpuUsage { get; set; }
    [CliOption(Description = nameof(CliTexts.GpuPowerOption), Required = false)] public double? GpuPower { get; set; }
    [CliOption(Description = nameof(CliTexts.GpuClockOption), Required = false)] public double? GpuClock { get; set; }
    [CliOption(Description = nameof(CliTexts.FanOption), Required = false)] public double? Fan { get; set; }
    [CliOption(Description = nameof(CliTexts.PumpOption), Required = false)] public double? Pump { get; set; }
    [CliOption(Description = nameof(CliTexts.RamOption), Required = false)] public double? Ram { get; set; }
    [CliOption(Description = nameof(CliTexts.TimeOption), Required = false)] public DateTime? Time { get; set; }
    [CliOption(Description = nameof(CliTexts.FahrenheitOption))] public bool Fahrenheit { get; set; }
    [CliOption(Description = nameof(CliTexts.DurationOption), Required = false)] public double? Duration { get; set; }
    [CliOption(Description = nameof(CliTexts.IntervalOption))] public int Interval { get; set; } = 250;
    [CliOption(Description = nameof(CliTexts.OffAtEndOption))] public bool OffAtEnd { get; set; }

    public Task<int> RunAsync(CliContext context)
    {
        var frame = new SegmentLcdFrame
        {
            CpuTemperature = CpuTemp,
            CpuUsage = CpuUsage,
            CpuPower = CpuPower,
            CpuClock = CpuClock,
            CpuVoltage = CpuVoltage,
            GpuTemperature = GpuTemp,
            GpuUsage = GpuUsage,
            GpuPower = GpuPower,
            GpuClock = GpuClock,
            FanSpeed = Fan,
            PumpSpeed = Pump,
            RamUsage = Ram,
            Time = Time,
            Unit = Fahrenheit ? TemperatureUnit.Fahrenheit : TemperatureUnit.Celsius,
        };

        return RunSessionAsync(
            SegmentLcdProtocol.EncodeUpdate(frame),
            Duration,
            Interval,
            OffAtEnd,
            context.CancellationToken,
            // Time == null -> re-encode every second so the clock keeps running.
            Time is null ? async (session, ct) =>
            {
                session.Update(frame);
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                while (await timer.WaitForNextTickAsync(ct))
                {
                }
            }
        : null);
    }
}

[CliCommand(Description = nameof(CliTexts.LiveDescription))]
public sealed class LiveCommand : DeviceCommandBase, ITemperatureUnitOption
{
    [CliOption(Description = nameof(CliTexts.CpuTempSensorOption), Required = false)]
    public string? CpuTempSensor { get; set; }

    [CliOption(Description = nameof(CliTexts.CpuPowerSensorOption), Required = false)]
    public string? CpuPowerSensor { get; set; }

    [CliOption(Description = nameof(CliTexts.CpuVoltageSensorOption), Required = false)]
    public string? CpuVoltageSensor { get; set; }

    [CliOption(Description = nameof(CliTexts.CpuClockModeOption))]
    public CpuClockMode CpuClockMode { get; set; } = CpuClockMode.Avg;

    [CliOption(Description = nameof(CliTexts.GpuTempSensorOption), Required = false)]
    public string? GpuTempSensor { get; set; }

    [CliOption(Description = nameof(CliTexts.GpuUsageSensorOption), Required = false)]
    public string? GpuUsageSensor { get; set; }

    [CliOption(Description = nameof(CliTexts.GpuPowerSensorOption), Required = false)]
    public string? GpuPowerSensor { get; set; }

    [CliOption(Description = nameof(CliTexts.GpuClockSensorOption), Required = false)]
    public string? GpuClockSensor { get; set; }

    [CliOption(Description = nameof(CliTexts.FanSensorOption), Required = false)]
    public string? FanSensor { get; set; }

    [CliOption(Description = nameof(CliTexts.PumpSensorOption), Required = false)]
    public string? PumpSensor { get; set; }

    [CliOption(Description = nameof(CliTexts.FahrenheitOption))] public bool Fahrenheit { get; set; }
    [CliOption(Description = nameof(CliTexts.DurationOption), Required = false)] public double? Duration { get; set; }
    [CliOption(Description = nameof(CliTexts.IntervalOption))] public int Interval { get; set; } = 250;
    [CliOption(Description = nameof(CliTexts.SampleIntervalOption))] public int SampleInterval { get; set; } = 1000;
    [CliOption(Description = nameof(CliTexts.ShowOption))] public bool Show { get; set; }

    public Task<int> RunAsync(CliContext context)
    {
        var source = new LinuxFrameSource(new SensorSettings
        {
            CpuTemperature = CpuTempSensor,
            CpuPower = CpuPowerSensor,
            CpuVoltage = CpuVoltageSensor,
            GpuTemperature = GpuTempSensor,
            GpuUsage = GpuUsageSensor,
            GpuPower = GpuPowerSensor,
            GpuClock = GpuClockSensor,
            Fan = FanSensor,
            Pump = PumpSensor,
            CpuClockMode = CpuClockMode,
        });

        foreach (var (field, description) in source.Sources)
            Console.Error.WriteLine($"{field,-16} {description}");
        Console.Error.WriteLine($"{"cpu_clock",-16} cpufreq scaling_cur_freq, {CpuClockMode}");
        if (FanSensor is null || PumpSensor is null)
            Console.Error.WriteLine(L.T("Note: specify fan/pump with --fan-sensor/--pump-sensor (see 'seglcd sensors')."));

        var unit = Fahrenheit ? TemperatureUnit.Fahrenheit : TemperatureUnit.Celsius;
        SegmentLcdFrame Sample() => source.Sample() with { Unit = unit };

        var first = Sample();
        return RunSessionAsync(SegmentLcdProtocol.EncodeUpdate(first), Duration, Interval, offAtEnd: true, context.CancellationToken,
            async (session, ct) =>
            {
                session.Update(first);
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(SampleInterval));
                while (await timer.WaitForNextTickAsync(ct))
                {
                    var frame = Sample();
                    session.Update(frame);
                    if (Show)
                        Console.WriteLine(
                            $"CPU {V(frame.CpuTemperature, "F1")}°C {V(frame.CpuUsage, "F0")}% {V(frame.CpuPower, "F1")}W {V(frame.CpuClock, "F0")}MHz {V(frame.CpuVoltage, "F2")}V"
                            + $" | GPU {V(frame.GpuTemperature, "F1")}°C {V(frame.GpuUsage, "F0")}% {V(frame.GpuPower, "F0")}W {V(frame.GpuClock, "F0")}MHz"
                            + $" | Fan {V(frame.FanSpeed, "F0")} RPM, Pump {V(frame.PumpSpeed, "F0")} RPM | RAM {V(frame.RamUsage, "F1")}%");
                }
            });
    }

    private static string V(double? value, string format) =>
        value is { } v ? v.ToString(format, CultureInfo.InvariantCulture) : "–";
}

[CliCommand(Description = nameof(CliTexts.ProbeDescription))]
public sealed class ProbeCommand : DeviceCommandBase
{
    [CliOption(Description = nameof(CliTexts.ProbeValueOption))]
    public byte Value { get; set; } = 88;

    [CliOption(Description = nameof(CliTexts.ProbeValuesOption), Required = false)]
    public string? Values { get; set; }

    [CliOption(Description = nameof(CliTexts.ProbeValueSecondsOption))]
    public double ValueSeconds { get; set; } = 1;

    [CliOption(Description = nameof(CliTexts.ProbeOffsetsOption), Required = false)]
    public string? Offsets { get; set; }

    [CliOption(Description = nameof(CliTexts.ProbeAutoOption), Required = false)]
    public double? Auto { get; set; }

    [CliOption(Description = nameof(CliTexts.ProbeNoHeaderOption))]
    public bool NoHeader { get; set; }

    public Task<int> RunAsync(CliContext context)
    {
        var offsets = Offsets is null
            ? Enumerable.Range(SegmentLcdProtocol.Offsets.CpuTemperatureWhole, SegmentLcdProtocol.PacketLength - SegmentLcdProtocol.Offsets.CpuTemperatureWhole).ToList()
            : Offsets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .SelectMany(o => o.Split('-') is [var from, var to]
                    ? Enumerable.Range(int.Parse(from, CultureInfo.InvariantCulture), int.Parse(to, CultureInfo.InvariantCulture) - int.Parse(from, CultureInfo.InvariantCulture) + 1)
                    : [int.Parse(o, CultureInfo.InvariantCulture)])
                .Where(o => o is >= 0 and < SegmentLcdProtocol.PacketLength)
                .ToList();

        // A sequence of values per byte (e.g. 0,1,50,99,128,255) instead of a single value.
        var sequence = Values?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => byte.Parse(v, CultureInfo.InvariantCulture))
            .ToArray();
        if (sequence is { Length: 0 })
            sequence = null;
        var step = TimeSpan.FromSeconds(Math.Max(0.1, ValueSeconds));
        var value = Value;

        byte[] Build(int? offset, byte v)
        {
            var p = new byte[SegmentLcdProtocol.PacketLength];
            if (!NoHeader)
            {
                p[SegmentLcdProtocol.Offsets.Command] = SegmentLcdProtocol.CommandUpdate;
                p[SegmentLcdProtocol.Offsets.SubCommand] = SegmentLcdProtocol.SubCommandUpdate;
            }

            if (offset is { } o)
                p[o] = v;
            return p;
        }

        void Show(int offset, byte v, bool newLine) =>
            Console.Write($"\rByte {offset,2} = {v,3} (0x{Hex.Byte(v)})  {FieldTexts.Name(offset),-40}{(newLine ? "\n" : " ")}");

        return RunSessionAsync(Build(null, 0), duration: null, intervalMs: 250, offAtEnd: true, context.CancellationToken,
            async (session, ct) =>
            {
                Console.WriteLine(L.T("Baseline: all values 0. Watch the display."));
                if (sequence is not null)
                    Console.WriteLine(L.T("Value sequence per byte: {0} ({1} s each).", string.Join(", ", sequence), step.TotalSeconds));
                Console.WriteLine(Auto is { } seconds
                    // translators: keys of the interactive probe; keep Enter, b, o and q as they are
                    ? L.T("Automatic run, {0} s per byte. Ctrl+C stops.", seconds)
                    : L.T("Enter = next byte, b = back, <number> = change value, o<offset> = jump, q = quit"));
                await Pause(ct, Auto);

                for (var i = 0; i < offsets.Count && !ct.IsCancellationRequested; i++)
                {
                    var o = offsets[i];
                    string input;
                    if (sequence is not null)
                    {
                        // Cycle through the sequence: once with --auto, otherwise until Enter is pressed.
                        var read = Auto is null ? Console.In.ReadLineAsync(ct).AsTask() : null;
                        do
                        {
                            foreach (var v in sequence)
                            {
                                session.UpdateRaw(Build(o, v));
                                Show(o, v, newLine: false);
                                await Task.WhenAny(Task.Delay(step, ct), read ?? Task.Delay(Timeout.Infinite, ct));
                                if (read is { IsCompleted: true } || ct.IsCancellationRequested)
                                    break;
                            }
                        }
                        while (read is { IsCompleted: false } && !ct.IsCancellationRequested);

                        Console.WriteLine();
                        if (read is null)
                            continue;
                        input = (await read)?.Trim() ?? "q";
                    }
                    else
                    {
                        session.UpdateRaw(Build(o, value));
                        Show(o, value, newLine: Auto is not null);
                        if (Auto is not null)
                        {
                            await Pause(ct, Auto);
                            continue;
                        }

                        Console.Write("> ");
                        input = (await Console.In.ReadLineAsync(ct))?.Trim() ?? "q";
                    }

                    if (input == "q")
                        break;
                    if (input == "b")
                        i = Math.Max(-1, i - 2);
                    else if (input.StartsWith('o') && int.TryParse(input[1..], CultureInfo.InvariantCulture, out var jump))
                        i = offsets.IndexOf(jump) is var idx and >= 0 ? idx - 1 : i - 1;
                    else if (byte.TryParse(input, CultureInfo.InvariantCulture, out var newValue))
                    {
                        value = newValue;
                        i--;
                    }
                }
            });
    }

    private static async Task Pause(CancellationToken ct, double? auto)
    {
        if (auto is { } s)
            await Task.Delay(TimeSpan.FromSeconds(s), ct);
        else
            await Console.In.ReadLineAsync(ct);
    }
}

[CliCommand(Description = nameof(CliTexts.SweepDescription))]
public sealed class SweepCommand : DeviceCommandBase
{
    [CliArgument(Description = nameof(CliTexts.SweepOffsetArgument))]
    public int Offset { get; set; }

    [CliOption(Description = nameof(CliTexts.SweepFromOption))] public byte From { get; set; }
    [CliOption(Description = nameof(CliTexts.SweepToOption))] public byte To { get; set; } = 100;
    [CliOption(Description = nameof(CliTexts.SweepStepOption))] public byte Step { get; set; } = 1;
    [CliOption(Description = nameof(CliTexts.SweepStepMsOption))] public int StepMs { get; set; } = 500;

    [CliOption(Description = nameof(CliTexts.SweepSetOption), Required = false)]
    public string? Set { get; set; }

    public Task<int> RunAsync(CliContext context)
    {
        var baseline = SegmentLcdProtocol.EncodeUpdate(new SegmentLcdFrame());
        foreach (var pair in (Set ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = pair.Split('=');
            baseline[int.Parse(kv[0], CultureInfo.InvariantCulture)] = byte.Parse(kv[1], CultureInfo.InvariantCulture);
        }

        return RunSessionAsync(baseline, duration: null, intervalMs: 250, offAtEnd: true, context.CancellationToken,
            async (session, ct) =>
            {
                for (int v = From; v <= To && !ct.IsCancellationRequested; v += Math.Max((byte)1, Step))
                {
                    var p = (byte[])baseline.Clone();
                    p[Offset] = (byte)v;
                    session.UpdateRaw(p);
                    Console.WriteLine($"Byte {Offset} = {v}");
                    await Task.Delay(StepMs, ct);
                }
            });
    }
}

[CliCommand(Description = nameof(CliTexts.RawDescription))]
public sealed class RawCommand : DeviceCommandBase
{
    [CliArgument(Description = nameof(CliTexts.RawBytesArgument))]
    public string[] Bytes { get; set; } = [];

    [CliOption(Description = nameof(CliTexts.RawOnceOption))] public bool Once { get; set; }
    [CliOption(Description = nameof(CliTexts.DurationOption), Required = false)] public double? Duration { get; set; }
    [CliOption(Description = nameof(CliTexts.IntervalOption))] public int Interval { get; set; } = 250;

    public Task<int> RunAsync(CliContext context)
    {
        var data = Hex.Parse(Bytes);
        if (data.Length > SegmentLcdProtocol.PacketLength)
        {
            Console.Error.WriteLine(L.T("At most {0} bytes.", SegmentLcdProtocol.PacketLength));
            return Task.FromResult(1);
        }

        var packet = new byte[SegmentLcdProtocol.PacketLength];
        data.CopyTo(packet, 0);

        if (Once && !DryRun)
        {
            using var device = OpenDevice();
            if (device is null)
                return Task.FromResult(2);
            device.SendPacket(packet);
            Console.WriteLine(Hex.Format(packet, trimTrailingZeros: false));
            return Task.FromResult(0);
        }

        return RunSessionAsync(packet, Duration, Interval, offAtEnd: false, context.CancellationToken);
    }
}

[CliCommand(Description = nameof(CliTexts.OffDescription))]
public sealed class OffCommand : DeviceCommandBase
{
    public int Run()
    {
        var packet = SegmentLcdProtocol.EncodeOff();
        if (DryRun)
        {
            Console.WriteLine(Hex.Format(packet, trimTrailingZeros: false));
            return 0;
        }

        using var device = OpenDevice();
        if (device is null)
            return 2;
        device.SendPacket(packet);
        return 0;
    }
}

[CliCommand(Description = nameof(CliTexts.ListenDescription))]
public sealed class ListenCommand : DeviceCommandBase
{
    [CliOption(Description = nameof(CliTexts.DurationOption), Required = false)] public double? Duration { get; set; }

    [CliOption(Description = nameof(CliTexts.ListenKeepAliveOption))]
    public bool KeepAlive { get; set; }

    public async Task<int> RunAsync(CliContext context)
    {
        using var device = OpenDevice();
        if (device is null)
            return 2;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        if (Duration is { } s)
            cts.CancelAfter(TimeSpan.FromSeconds(s));

        var keepAlive = KeepAlive
            ? Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(SegmentLcdProtocol.DefaultUpdateInterval);
                while (await timer.WaitForNextTickAsync(cts.Token).ConfigureAwait(false))
                    device.Send(new SegmentLcdFrame());
            })
            : Task.CompletedTask;

        Console.Error.WriteLine(L.T("Waiting for input reports … (Ctrl+C stops)"));
        var count = 0;
        await Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                var report = device.ReadInputReport(TimeSpan.FromMilliseconds(500));
                if (report is null)
                    continue;
                count++;
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  {Hex.Format(report, trimTrailingZeros: false)}");
            }
        });

        try
        {
            await keepAlive;
        }
        catch (OperationCanceledException)
        {
        }

        Console.Error.WriteLine(L.Tn("{0} input report received.", "{0} input reports received.", count, count));
        return 0;
    }
}

[CliCommand(Description = nameof(CliTexts.HoldDescription))]
public sealed class HoldCommand : DeviceCommandBase
{
    [CliOption(Description = nameof(CliTexts.HoldSendOption))] public double Send { get; set; } = 5;
    [CliOption(Description = nameof(CliTexts.HoldHoldOption))] public double Hold { get; set; } = 10;
    [CliOption(Description = nameof(CliTexts.HoldCpuTempOption))] public double CpuTemp { get; set; } = 42;
    [CliOption(Description = nameof(CliTexts.HoldGpuTempOption))] public double GpuTemp { get; set; } = 57;

    public async Task<int> RunAsync(CliContext context)
    {
        using var device = OpenDevice();
        if (device is null)
            return 2;

        var ct = context.CancellationToken;
        var frame = new SegmentLcdFrame { CpuTemperature = CpuTemp, GpuTemperature = GpuTemp };
        var stopSending = DateTime.Now.AddSeconds(Send);
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  " + L.T("Sending every 250 ms for {0} s …", Send));

        try
        {
            using var timer = new PeriodicTimer(SegmentLcdProtocol.DefaultUpdateInterval);
            do
            {
                device.Send(frame);
            }
            while (DateTime.Now < stopSending && await timer.WaitForNextTickAsync(ct));

            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  " + L.T("Last packet sent – the device stays open for {0} s. When does the display go off?", Hold));
            await Task.Delay(TimeSpan.FromSeconds(Hold), ct);
        }
        catch (OperationCanceledException)
        {
        }

        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  " + L.T("Closing the device."));
        return 0;
    }
}

[CliCommand(Description = nameof(CliTexts.SensorsDescription))]
public sealed class SensorsCommand
{
    private static IStringLocalizer L => Texts.Current;

    public async Task<int> RunAsync()
    {
        static string F(double? v) => v is { } x ? x.ToString("0.###", CultureInfo.InvariantCulture) : "–";
        static void Row(string spec, string description, string value) =>
            Console.WriteLine($"{spec,-36} {value,10}  {description}");

        Console.WriteLine("# " + L.T("hwmon (spec: hwmonN/name or chip:label)"));
        foreach (var s in LinuxSensors.EnumerateHwmon())
            Row($"{s.Chip}:{s.Label}", s.Id, F(s.Read()));

        Console.WriteLine();
        Console.WriteLine("# " + L.T("RAPL (spec: rapl, rapl:<domain>) – W, measured over 0.5 s"));
        var rapl = RaplPowerMeter.EnumerateDomains()
            .Select(d => (d.Name, d.Dir, Meter: RaplPowerMeter.TryCreate(d.Name)))
            .ToList();
        await Task.Delay(500);
        foreach (var (name, dir, meter) in rapl)
            // translators: shown instead of a value when the RAPL counter is not readable
            Row($"rapl:{name.Split('-')[0]}", dir, meter is null ? L.T("(permissions)") : F(meter.Read()));
        if (rapl.Count == 0)
            Console.WriteLine(L.T("(no RAPL domains)"));

        Console.WriteLine();
        Console.WriteLine("# " + L.T("NVIDIA NVML (spec: nvml[N]:temperature|power|clock|memclock|usage|fan)"));
        var nvml = NvmlSensor.EnumerateAll().ToList();
        foreach (var (spec, description, value) in nvml)
            Row(spec, description, F(value));
        if (nvml.Count == 0)
            Console.WriteLine(L.T("(no NVIDIA GPU, or libnvidia-ml.so.1 not found)"));

        Console.WriteLine();
        Console.WriteLine("# " + L.T("DRM (spec: drm[N]:busy|freq)"));
        var drm = DrmSensor.EnumerateAll().ToList();
        foreach (var (spec, description, value) in drm)
            Row(spec, description, F(value));
        if (drm.Count == 0)
            Console.WriteLine(L.T("(no matching DRM values)"));

        Console.WriteLine();
        Console.WriteLine("# " + L.T("built in"));
        Row("(cpufreq) " + L.T("CPU clock MHz"), "avg/min/max/cpu0",
            string.Join("/", new[] { CpuClockMode.Avg, CpuClockMode.Min, CpuClockMode.Max, CpuClockMode.First }.Select(m => LinuxSensors.CpuClock(m)?.ToString("0", CultureInfo.InvariantCulture) ?? "–")));
        Row("(meminfo) " + L.T("RAM %"), "/proc/meminfo", F(LinuxSensors.RamUsage()));
        return 0;
    }
}

[CliCommand(Description = nameof(CliTexts.DecodeDescription))]
public sealed class DecodeCommand
{
    private static IStringLocalizer L => Texts.Current;

    [CliArgument(Description = nameof(CliTexts.DecodeFileArgument), Arity = CliArgumentArity.ZeroOrOne)]
    public string File { get; set; } = "-";

    [CliOption(Description = nameof(CliTexts.DecodeDiffOption))]
    public bool Diff { get; set; }

    [CliOption(Description = nameof(CliTexts.DecodeStripReportIdOption))]
    public bool StripReportId { get; set; }

    public int Run()
    {
        var reader = File == "-" ? Console.In : new StreamReader(File);
        byte[]? previous = null;
        var lineNo = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNo++;
            // tshark -T fields: "<time>\t<hex>" – take the last column
            var hexPart = line.Split('\t', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
            if (string.IsNullOrEmpty(hexPart) || hexPart.StartsWith('#'))
                continue;
            var prefix = line.Contains('\t') ? line[..line.LastIndexOf('\t')].Trim() + "  " : string.Empty;

            byte[] data;
            try
            {
                data = Hex.Parse([hexPart]);
            }
            catch (FormatException)
            {
                Console.Error.WriteLine(L.T("Line {0}: not hex – skipped.", lineNo));
                continue;
            }

            if (StripReportId && data.Length == SegmentLcdProtocol.HidWriteLength)
                data = data[1..];
            if (data.Length < SegmentLcdProtocol.PacketLength)
                Array.Resize(ref data, SegmentLcdProtocol.PacketLength); // tshark may omit trailing bytes

            Console.WriteLine($"{prefix}#{lineNo}: {Describe(data)}");
            for (var i = 0; i < data.Length; i++)
            {
                if (Diff && previous is not null && i < previous.Length && previous[i] == data[i])
                    continue;
                if (!Diff && i >= SegmentLcdProtocol.Offsets.PaddingStart && data[i] == 0)
                    continue;
                var name = FieldTexts.Name(i);
                var change = Diff && previous is not null && i < previous.Length ? "  " + L.T("(before {0})", previous[i]) : string.Empty;
                Console.WriteLine($"    [{i,2}] 0x{Hex.Byte(data[i])} {data[i],3}  {name}{change}");
            }

            if (data.Length > 2 && data[1] == SegmentLcdProtocol.CommandUpdate && data[2] == SegmentLcdProtocol.SubCommandUpdate && data.Length >= SegmentLcdProtocol.Offsets.PaddingStart)
                Console.WriteLine("    => " + Summary(data));
            previous = data;
        }

        return 0;
    }

    private static string Describe(byte[] d) => d.Length switch
    {
        > 2 when d[1] == SegmentLcdProtocol.CommandUpdate && d[2] == SegmentLcdProtocol.SubCommandUpdate => L.T("Update ({0} bytes)", d.Length),
        > 1 when d[1] == SegmentLcdProtocol.CommandOff => L.T("Off ({0} bytes)", d.Length),
        > 2 => L.T("Unknown command {0} {1} ({2} bytes)", Hex.Byte(d[1]), Hex.Byte(d[2]), d.Length),
        _ => L.T("{0} bytes", d.Length),
    };

    private static string Summary(byte[] d) => string.Create(CultureInfo.InvariantCulture,
        $"CPU {d[3]}.{d[4]:00}°{(d[5] == 1 ? 'F' : 'C')} {d[6]}.{d[48]:00}% {d[34] * 100 + d[7]}.{d[8]:00}W {d[9] * 100 + d[10]}MHz {d[11]}.{d[12]:00}V | " +
        $"GPU {d[13]}.{d[14]:00}°{(d[15] == 1 ? 'F' : 'C')} {d[16]}% {d[35] * 100 + d[17]}.{d[18]:00}W {d[19] * 100 + d[20]}MHz | " +
        $"Fan {d[21] * 100 + d[22]} Pump {d[23] * 100 + d[24]} | {d[25] * 100 + d[26]:0000}-{d[27]:00}-{d[28]:00} {d[29]:00}:{d[30]:00}:{d[31]:00} dow{d[32]} | RAM {d[33]}.{d[50]:00}%");
}