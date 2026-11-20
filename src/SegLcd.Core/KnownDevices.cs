namespace SegLcd;

/// <summary>A USB device that is known (or assumed) to speak the segment-LCD protocol.</summary>
public sealed record KnownDevice(ushort VendorId, ushort ProductId, string Name, bool ProtocolVerified)
{
    /// <summary>
    /// <c>false</c> if the display mis-renders temperatures sent with the °F flag
    /// (LB360-S: no unit change, values ≥ 100 shown modulo 100). Send °C to such devices.
    /// </summary>
    public bool FahrenheitSupported { get; init; } = true;
}

public static class KnownDevices
{
    public const ushort VendorId = 0x5131;

    /// <summary>
    /// LB360-S ARGB Sync (CL-W499 / CL-W500). Update packet verified on hardware (2026-11-20):
    /// shows the integer CPU and GPU temperature; decimals, °F flag and CPU usage have no visible effect.
    /// </summary>
    public static readonly KnownDevice Lb360S = new(VendorId, 0x200E, "Thermaltake LB360-S ARGB Sync", ProtocolVerified: true)
    {
        FahrenheitSupported = false,
    };

    /// <summary>LA series water block; protocol verified by the ThermalTakeWaKue project.</summary>
    public static readonly KnownDevice LaSeries = new(VendorId, 0x2007, "Thermaltake LA series", ProtocolVerified: true);

    public static IReadOnlyList<KnownDevice> All { get; } = [Lb360S, LaSeries];

    public static KnownDevice? Find(ushort vendorId, ushort productId) =>
        All.FirstOrDefault(d => d.VendorId == vendorId && d.ProductId == productId);
}