using DotMake.CommandLine;

using Microsoft.Extensions.Localization;

using SegLcd.Localization;

namespace SegLcd.Tool;

/// <summary>Implemented by commands with a <c>--fahrenheit</c> option.</summary>
public interface ITemperatureUnitOption
{
    bool Fahrenheit { get; }
}

/// <summary>Shared options of all commands that talk to the device.</summary>
public abstract class DeviceCommandBase
{
    [CliOption(Description = nameof(CliTexts.DeviceOption), Required = false)]
    public string? Device { get; set; }

    [CliOption(Description = nameof(CliTexts.DryRunOption))]
    public bool DryRun { get; set; }

    [CliOption(Description = nameof(CliTexts.VerboseOption))]
    public bool Verbose { get; set; }

    protected static IStringLocalizer L => Texts.Current;

    protected SegmentLcdDevice? OpenDevice()
    {
        var device = SegmentLcdDevice.TryOpenFirst(Device);
        if (device is not null)
        {
            Console.Error.WriteLine(device.Model.ProtocolVerified
                ? L.T("Connected: {0} ({1})", device.Model.Name, device.DevicePath)
                : L.T("Connected: {0} ({1}) – protocol unverified for this model", device.Model.Name, device.DevicePath));
            if (this is ITemperatureUnitOption { Fahrenheit: true } && !device.Model.FahrenheitSupported)
                Console.Error.WriteLine(L.T("Warning: {0} does not display °F (the symbol stays °C, values ≥ 100 appear with two digits, e.g. 122 → 22). Send without --fahrenheit.", device.Model.Name));
            return device;
        }

        var candidates = SegmentLcdDevice.Enumerate();
        Console.Error.WriteLine(candidates.Count == 0
            ? L.T("No segment LCD found (VID 0x5131, PID 0x200E/0x2007).")
            : L.T("Device found ({0}), but it could not be opened – is the udev rule installed? See udev/70-seglcd.rules", string.Join(", ", candidates.Select(c => c.Path))));
        return null;
    }

    /// <summary>
    /// Sends <paramref name="initialPacket"/> periodically (keep-alive) until <paramref name="duration"/>
    /// elapses or Ctrl+C. An optional <paramref name="driver"/> can change the packet while running.
    /// </summary>
    protected async Task<int> RunSessionAsync(
        byte[] initialPacket,
        double? duration,
        int intervalMs,
        bool offAtEnd,
        CancellationToken ct,
        Func<SegmentLcdSession, CancellationToken, Task>? driver = null)
    {
        if (DryRun)
        {
            Console.WriteLine(Hex.Format(initialPacket, trimTrailingZeros: false));
            return 0;
        }

        if (intervalMs > SegmentLcdProtocol.MaxReliableUpdateInterval.TotalMilliseconds)
            Console.Error.WriteLine(L.T("Warning: interval {0} ms > {1} ms – the display turns off after about 400–500 ms without a packet and will flicker.", intervalMs, SegmentLcdProtocol.MaxReliableUpdateInterval.TotalMilliseconds));

        var device = OpenDevice();
        if (device is null)
            return 2;

        // First call hands out the already opened device, later calls reconnect after unplug.
        SegmentLcdDevice? pending = device;
        await using var session = new SegmentLcdSession(
            () =>
            {
                var d = pending ?? SegmentLcdDevice.TryOpenFirst(Device);
                pending = null;
                return d;
            },
            TimeSpan.FromMilliseconds(intervalMs),
            offAtEnd);
        session.UpdateRaw(initialPacket);
        if (Verbose)
            session.PacketSent += p => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  {Hex.Format(p)}");
        session.ConnectionChanged += path => Console.Error.WriteLine(path is null ? L.T("Connection lost.") : L.T("Connected: {0}", path));
        session.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (duration is { } seconds)
            cts.CancelAfter(TimeSpan.FromSeconds(seconds));
        else if (driver is null)
            Console.Error.WriteLine(L.T("Sending … (Ctrl+C stops)"));

        try
        {
            if (driver is not null)
                await driver(session, cts.Token);
            else
                await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        return 0;
    }
}