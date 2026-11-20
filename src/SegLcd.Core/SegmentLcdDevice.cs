using HidApi;

namespace SegLcd;

/// <summary>
/// A connected Thermaltake segment LCD (vendor-defined HID device, 64-byte reports, no report IDs).
/// Backed by HidApi.Net, i.e. the native hidapi library (<c>libhidapi-hidraw.so.0</c> on Linux,
/// <c>hidapi.dll</c> on Windows), which must be installed or shipped with the application.
/// </summary>
/// <remarks>Writes are serialised internally; the instance can be shared between threads.</remarks>
public sealed class SegmentLcdDevice : IDisposable
{
    private readonly Device _device;
    private readonly Lock _writeLock = new();
    private bool _disposed;

    private SegmentLcdDevice(DeviceInfo info, Device device, KnownDevice model)
    {
        Info = info;
        _device = device;
        Model = model;
    }

    /// <summary>hidapi device information (path, IDs, usage page, bus type).</summary>
    public DeviceInfo Info { get; }

    /// <summary>Which known model was matched.</summary>
    public KnownDevice Model { get; }

    /// <summary>Platform path, e.g. <c>/dev/hidraw5</c> on Linux.</summary>
    public string DevicePath => Info.Path;

    /// <summary>
    /// All HID interfaces that match an entry of <see cref="KnownDevices.All"/>.
    /// Does not open the devices, so it also lists devices without access permission.
    /// </summary>
    public static IReadOnlyList<DeviceInfo> Enumerate() =>
        KnownDevices.All
            .SelectMany(k => Hid.Enumerate(k.VendorId, k.ProductId))
            .ToList();

    /// <summary>Opens the first matching device, or returns <c>null</c> if none is present/accessible.</summary>
    public static SegmentLcdDevice? TryOpenFirst(string? devicePath = null)
    {
        foreach (var info in Enumerate())
        {
            if (devicePath is not null && !string.Equals(info.Path, devicePath, StringComparison.Ordinal))
                continue;
            var opened = TryOpen(info);
            if (opened is not null)
                return opened;
        }

        return null;
    }

    /// <summary>Opens <paramref name="info"/>; returns <c>null</c> if it cannot be opened (e.g. missing permissions).</summary>
    public static SegmentLcdDevice? TryOpen(DeviceInfo info)
    {
        var model = KnownDevices.Find(info.VendorId, info.ProductId)
                    ?? new KnownDevice(info.VendorId, info.ProductId, "Unknown", false);
        try
        {
            return new SegmentLcdDevice(info, info.ConnectToDevice(), model);
        }
        catch (HidException)
        {
            return null;
        }
    }

    /// <summary>Sends one 64-byte protocol packet (the report-ID byte 0x00 is added here).</summary>
    /// <exception cref="IOException">Device disconnected or write failed.</exception>
    public void SendPacket(ReadOnlySpan<byte> packet)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Span<byte> report = stackalloc byte[SegmentLcdProtocol.HidWriteLength];
        report.Clear();
        packet[..Math.Min(packet.Length, SegmentLcdProtocol.PacketLength)].CopyTo(report[1..]);
        try
        {
            lock (_writeLock)
                _device.Write(report);
        }
        catch (HidException e)
        {
            throw new IOException($"Write to {DevicePath} failed: {e.Message}", e);
        }
    }

    /// <summary>Encodes and sends an update packet.</summary>
    public void Send(SegmentLcdFrame frame) => SendPacket(SegmentLcdProtocol.EncodeUpdate(frame));

    /// <summary>Sends the "display off" packet.</summary>
    public void SendOff() => SendPacket(SegmentLcdProtocol.EncodeOff());

    /// <summary>
    /// Reads one input report. Returns the payload (hidapi strips report ID 0, so normally 64 bytes),
    /// or <c>null</c> on timeout.
    /// </summary>
    /// <exception cref="IOException">Device disconnected or read failed.</exception>
    public byte[]? ReadInputReport(TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var buffer = new byte[SegmentLcdProtocol.HidWriteLength];
        try
        {
            var n = _device.ReadTimeout(buffer, (int)Math.Clamp(timeout.TotalMilliseconds, 0, int.MaxValue));
            return n == 0 ? null : buffer.AsSpan(0, n).ToArray();
        }
        catch (HidException e)
        {
            throw new IOException($"Read from {DevicePath} failed: {e.Message}", e);
        }
    }

    /// <summary>Raw HID report descriptor as reported by hidapi (≥ 0.14).</summary>
    public byte[] GetReportDescriptor()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _device.GetReportDescriptor().ToArray();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _device.Dispose();
    }
}