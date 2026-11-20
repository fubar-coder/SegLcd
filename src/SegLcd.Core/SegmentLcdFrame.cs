namespace SegLcd;

/// <summary>Temperature unit flag transmitted with CPU and GPU temperature.</summary>
public enum TemperatureUnit : byte
{
    Celsius = 0,
    Fahrenheit = 1,
}

/// <summary>
/// One set of values for the segment LCD.
/// </summary>
/// <remarks>
/// Temperatures are always given in °C; conversion to °F happens while encoding
/// (see <see cref="Unit"/>). <c>null</c>, NaN, infinite and negative values are sent as 0.
/// If <see cref="Time"/> is <c>null</c>, the encoder uses the time passed to
/// <see cref="SegmentLcdProtocol.EncodeUpdate(SegmentLcdFrame, Span{byte}, DateTime?)"/>
/// or <see cref="DateTime.Now"/>.
/// </remarks>
public sealed record SegmentLcdFrame
{
    /// <summary>CPU temperature in °C (0.00 – 255.99).</summary>
    public double? CpuTemperature { get; init; }

    /// <summary>CPU usage in percent (0.00 – 100.00), sent with two decimals.</summary>
    public double? CpuUsage { get; init; }

    /// <summary>CPU package power in watts (0.00 – 9999.99).</summary>
    public double? CpuPower { get; init; }

    /// <summary>CPU clock in MHz (0 – 9999).</summary>
    public double? CpuClock { get; init; }

    /// <summary>CPU core voltage in volts (0.00 – 255.99).</summary>
    public double? CpuVoltage { get; init; }

    /// <summary>GPU temperature in °C (0.00 – 255.99).</summary>
    public double? GpuTemperature { get; init; }

    /// <summary>GPU usage in percent (0 – 100).</summary>
    public double? GpuUsage { get; init; }

    /// <summary>GPU power in watts (0.00 – 9999.99).</summary>
    public double? GpuPower { get; init; }

    /// <summary>GPU clock in MHz (0 – 9999).</summary>
    public double? GpuClock { get; init; }

    /// <summary>Fan speed in RPM (0 – 9999).</summary>
    public double? FanSpeed { get; init; }

    /// <summary>Pump speed in RPM (0 – 9999).</summary>
    public double? PumpSpeed { get; init; }

    /// <summary>RAM usage in percent (0.00 – 100.00), sent with two decimals.</summary>
    public double? RamUsage { get; init; }

    /// <summary>Local wall-clock time shown by displays with a clock.</summary>
    public DateTime? Time { get; init; }

    /// <summary>Unit in which the temperatures are transmitted.</summary>
    public TemperatureUnit Unit { get; init; } = TemperatureUnit.Celsius;
}