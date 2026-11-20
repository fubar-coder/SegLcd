namespace SegLcd;

/// <summary>
/// Encoder for the 64-byte HID output reports of the Thermaltake segment LCD.
/// The byte layout is documented in <c>docs/content/docs/protokoll/update-paket.md</c>.
/// </summary>
public static class SegmentLcdProtocol
{
    /// <summary>Length of one output report on the wire (without the HID report-ID byte).</summary>
    public const int PacketLength = 64;

    /// <summary>Length of a write to hidraw/hidapi: report ID 0x00 + <see cref="PacketLength"/>.</summary>
    public const int HidWriteLength = PacketLength + 1;

    /// <summary>Interval used by the official software between two update packets.</summary>
    public static readonly TimeSpan DefaultUpdateInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Longest interval that kept the LB360-S display on (measured: 400 ms ok, 500 ms flickers).
    /// The display turns off roughly 400–500 ms after the last update packet.
    /// </summary>
    public static readonly TimeSpan MaxReliableUpdateInterval = TimeSpan.FromMilliseconds(400);

    public const byte CommandUpdate = 0x01;
    public const byte SubCommandUpdate = 0x02;
    public const byte CommandOff = 0x0F;

    /// <summary>Absolute byte offsets inside the 64-byte packet.</summary>
    public static class Offsets
    {
        public const int Reserved0 = 0;
        public const int Command = 1;
        public const int SubCommand = 2;
        public const int CpuTemperatureWhole = 3;
        public const int CpuTemperatureFraction = 4;
        public const int CpuTemperatureUnit = 5;
        public const int CpuUsage = 6;
        public const int CpuPowerTensAndOnes = 7;
        public const int CpuPowerFraction = 8;
        public const int CpuClockHigh = 9;
        public const int CpuClockLow = 10;
        public const int CpuVoltageWhole = 11;
        public const int CpuVoltageFraction = 12;
        public const int GpuTemperatureWhole = 13;
        public const int GpuTemperatureFraction = 14;
        public const int GpuTemperatureUnit = 15;
        public const int GpuUsage = 16;
        public const int GpuPowerTensAndOnes = 17;
        public const int GpuPowerFraction = 18;
        public const int GpuClockHigh = 19;
        public const int GpuClockLow = 20;
        public const int FanSpeedHigh = 21;
        public const int FanSpeedLow = 22;
        public const int PumpSpeedHigh = 23;
        public const int PumpSpeedLow = 24;
        public const int YearCentury = 25;
        public const int YearOfCentury = 26;
        public const int Month = 27;
        public const int Day = 28;
        public const int Hour = 29;
        public const int Minute = 30;
        public const int Second = 31;
        public const int DayOfWeek = 32;
        public const int RamUsage = 33;
        public const int CpuPowerHundreds = 34;
        public const int GpuPowerHundreds = 35;

        /// <summary>
        /// Start of the extended area (36 – 63). Mostly 0; the original LB360-S software fills
        /// <see cref="Unknown37"/>, <see cref="CpuUsageFraction"/> and <see cref="RamUsageFraction"/>.
        /// </summary>
        public const int PaddingStart = 36;

        /// <summary>Unknown; original LB360-S software sends a constant 5. Not written by the encoder.</summary>
        public const int Unknown37 = 37;

        /// <summary>CPU usage, hundredths (0–99) – identified from LB360-S captures (0 at exactly 100 %).</summary>
        public const int CpuUsageFraction = 48;

        /// <summary>RAM usage, hundredths (0–99) – identified from LB360-S captures (continuous with byte 33).</summary>
        public const int RamUsageFraction = 50;
    }

    /// <summary>English names of the bytes (for tools; the CLI shows translated names).</summary>
    public static readonly IReadOnlyDictionary<int, string> FieldNames = new Dictionary<int, string>
    {
        [Offsets.CpuTemperatureWhole] = "CPU temperature (integer part)",
        [Offsets.CpuTemperatureFraction] = "CPU temperature (hundredths)",
        [Offsets.CpuTemperatureUnit] = "CPU temperature unit (0=°C, 1=°F)",
        [Offsets.CpuUsage] = "CPU usage % (integer part)",
        [Offsets.CpuPowerTensAndOnes] = "CPU power W mod 100",
        [Offsets.CpuPowerFraction] = "CPU power (hundredths)",
        [Offsets.CpuClockHigh] = "CPU clock MHz / 100",
        [Offsets.CpuClockLow] = "CPU clock MHz mod 100",
        [Offsets.CpuVoltageWhole] = "CPU voltage (integer part)",
        [Offsets.CpuVoltageFraction] = "CPU voltage (hundredths)",
        [Offsets.GpuTemperatureWhole] = "GPU temperature (integer part)",
        [Offsets.GpuTemperatureFraction] = "GPU temperature (hundredths)",
        [Offsets.GpuTemperatureUnit] = "GPU temperature unit (0=°C, 1=°F)",
        [Offsets.GpuUsage] = "GPU usage %",
        [Offsets.GpuPowerTensAndOnes] = "GPU power W mod 100",
        [Offsets.GpuPowerFraction] = "GPU power (hundredths)",
        [Offsets.GpuClockHigh] = "GPU clock MHz / 100",
        [Offsets.GpuClockLow] = "GPU clock MHz mod 100",
        [Offsets.FanSpeedHigh] = "fan RPM / 100",
        [Offsets.FanSpeedLow] = "fan RPM mod 100",
        [Offsets.PumpSpeedHigh] = "pump RPM / 100",
        [Offsets.PumpSpeedLow] = "pump RPM mod 100",
        [Offsets.YearCentury] = "year / 100",
        [Offsets.YearOfCentury] = "year mod 100",
        [Offsets.Month] = "month",
        [Offsets.Day] = "day",
        [Offsets.Hour] = "hour",
        [Offsets.Minute] = "minute",
        [Offsets.Second] = "second",
        [Offsets.DayOfWeek] = "day of week (0=Sunday)",
        [Offsets.RamUsage] = "RAM usage % (integer part)",
        [Offsets.CpuPowerHundreds] = "CPU power W / 100",
        [Offsets.GpuPowerHundreds] = "GPU power W / 100",
        [Offsets.Unknown37] = "unknown (original LB360-S software: always 5)",
        [Offsets.CpuUsageFraction] = "CPU usage (hundredths)",
        [Offsets.RamUsageFraction] = "RAM usage (hundredths)",
    };

    /// <summary>Encodes an update packet into a new 64-byte array.</summary>
    public static byte[] EncodeUpdate(SegmentLcdFrame frame, DateTime? now = null)
    {
        var packet = new byte[PacketLength];
        EncodeUpdate(frame, packet, now);
        return packet;
    }

    /// <summary>Encodes an update packet into <paramref name="destination"/> (at least 64 bytes).</summary>
    public static void EncodeUpdate(SegmentLcdFrame frame, Span<byte> destination, DateTime? now = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (destination.Length < PacketLength)
            throw new ArgumentException($"Destination must be at least {PacketLength} bytes.", nameof(destination));

        var p = destination[..PacketLength];
        p.Clear();
        p[Offsets.Command] = CommandUpdate;
        p[Offsets.SubCommand] = SubCommandUpdate;

        var unit = (byte)frame.Unit;

        WriteFixed2(p, Offsets.CpuTemperatureWhole, Offsets.CpuTemperatureFraction, ToUnit(frame.CpuTemperature, frame.Unit), 255);
        p[Offsets.CpuTemperatureUnit] = unit;
        WriteFixed2(p, Offsets.CpuUsage, Offsets.CpuUsageFraction, frame.CpuUsage, 255);
        WritePower(p, Offsets.CpuPowerHundreds, Offsets.CpuPowerTensAndOnes, Offsets.CpuPowerFraction, frame.CpuPower);
        WriteBase100(p, Offsets.CpuClockHigh, Offsets.CpuClockLow, frame.CpuClock);
        WriteFixed2(p, Offsets.CpuVoltageWhole, Offsets.CpuVoltageFraction, frame.CpuVoltage, 255);

        WriteFixed2(p, Offsets.GpuTemperatureWhole, Offsets.GpuTemperatureFraction, ToUnit(frame.GpuTemperature, frame.Unit), 255);
        p[Offsets.GpuTemperatureUnit] = unit;
        p[Offsets.GpuUsage] = Integer(frame.GpuUsage, 255);
        WritePower(p, Offsets.GpuPowerHundreds, Offsets.GpuPowerTensAndOnes, Offsets.GpuPowerFraction, frame.GpuPower);
        WriteBase100(p, Offsets.GpuClockHigh, Offsets.GpuClockLow, frame.GpuClock);

        WriteBase100(p, Offsets.FanSpeedHigh, Offsets.FanSpeedLow, frame.FanSpeed);
        WriteBase100(p, Offsets.PumpSpeedHigh, Offsets.PumpSpeedLow, frame.PumpSpeed);

        var t = frame.Time ?? now ?? DateTime.Now;
        p[Offsets.YearCentury] = (byte)(t.Year / 100);
        p[Offsets.YearOfCentury] = (byte)(t.Year % 100);
        p[Offsets.Month] = (byte)t.Month;
        p[Offsets.Day] = (byte)t.Day;
        p[Offsets.Hour] = (byte)t.Hour;
        p[Offsets.Minute] = (byte)t.Minute;
        p[Offsets.Second] = (byte)t.Second;
        p[Offsets.DayOfWeek] = (byte)t.DayOfWeek; // Sunday = 0

        WriteFixed2(p, Offsets.RamUsage, Offsets.RamUsageFraction, frame.RamUsage, 255);
    }

    /// <summary>Encodes the "display off" packet (sent by the official software on exit).</summary>
    public static byte[] EncodeOff()
    {
        var p = new byte[PacketLength];
        p[Offsets.Command] = CommandOff;
        return p;
    }

    /// <summary>
    /// Prepends the HID report ID (0x00, device has no numbered reports), producing the
    /// 65-byte buffer expected by hidraw <c>write()</c>, hidapi and Windows <c>WriteFile</c>.
    /// </summary>
    public static byte[] ToHidReport(ReadOnlySpan<byte> packet)
    {
        if (packet.Length > PacketLength)
            throw new ArgumentException($"Packet must not exceed {PacketLength} bytes.", nameof(packet));
        var report = new byte[HidWriteLength];
        packet.CopyTo(report.AsSpan(1));
        return report;
    }

    private static double? ToUnit(double? celsius, TemperatureUnit unit) =>
        unit == TemperatureUnit.Fahrenheit && celsius is { } c ? c * 9.0 / 5.0 + 32.0 : celsius;

    /// <summary>Sanitises a value: null/NaN/∞/negative → 0.</summary>
    private static double Sanitize(double? value) =>
        value is { } v && double.IsFinite(v) && v > 0 ? v : 0;

    private static byte Integer(double? value, int max) =>
        (byte)Math.Min((long)Math.Floor(Sanitize(value)), max);

    /// <summary>Fixed point with two decimals: rounds to 1/100, then splits into whole and fraction.</summary>
    private static void WriteFixed2(Span<byte> p, int wholeOffset, int fractionOffset, double? value, int maxWhole)
    {
        var centi = (long)Math.Round(Sanitize(value) * 100, MidpointRounding.AwayFromZero);
        centi = Math.Min(centi, maxWhole * 100L + 99);
        p[wholeOffset] = (byte)(centi / 100);
        p[fractionOffset] = (byte)(centi % 100);
    }

    /// <summary>0 – 9999 as two base-100 digits (1234 → 12, 34).</summary>
    private static void WriteBase100(Span<byte> p, int highOffset, int lowOffset, double? value)
    {
        var v = Math.Min((long)Math.Floor(Sanitize(value)), 9999);
        p[highOffset] = (byte)(v / 100);
        p[lowOffset] = (byte)(v % 100);
    }

    /// <summary>0.00 – 9999.99 W as hundreds, tens+ones and fraction (123.45 → 1, 23, 45).</summary>
    private static void WritePower(Span<byte> p, int hundredsOffset, int lowOffset, int fractionOffset, double? value)
    {
        var centi = (long)Math.Round(Sanitize(value) * 100, MidpointRounding.AwayFromZero);
        centi = Math.Min(centi, 999_999);
        var whole = centi / 100;
        p[hundredsOffset] = (byte)(whole / 100);
        p[lowOffset] = (byte)(whole % 100);
        p[fractionOffset] = (byte)(centi % 100);
    }
}