namespace SegLcd.Tests;

public class SegmentLcdProtocolTests
{
    private static readonly DateTime FixedTime = new(2026, 11, 20, 13, 45, 30); // Friday

    [Test]
    public async Task Update_packet_has_header_and_length()
    {
        var p = SegmentLcdProtocol.EncodeUpdate(new SegmentLcdFrame { Time = FixedTime });

        await Assert.That(p.Length).IsEqualTo(64);
        await Assert.That(p[0]).IsEqualTo((byte)0x00);
        await Assert.That(p[1]).IsEqualTo((byte)0x01);
        await Assert.That(p[2]).IsEqualTo((byte)0x02);
    }

    [Test]
    public async Task Documented_example_is_reproduced()
    {
        // From the protocol docs: CPU 42.5 °C, 37 %, 123.45 W, 4567 MHz; fan 1234 RPM; pump 2345 RPM
        var p = SegmentLcdProtocol.EncodeUpdate(new SegmentLcdFrame
        {
            CpuTemperature = 42.5,
            CpuUsage = 37,
            CpuPower = 123.45,
            CpuClock = 4567,
            FanSpeed = 1234,
            PumpSpeed = 2345,
            Time = FixedTime,
        });

        byte[] expectedStart = [0x00, 0x01, 0x02, 0x2a, 0x32, 0x00, 0x25, 0x17, 0x2d, 0x2d, 0x43];
        await Assert.That(Convert.ToHexString(p, 0, expectedStart.Length)).IsEqualTo(Convert.ToHexString(expectedStart));
        await Assert.That(p[21]).IsEqualTo((byte)12);
        await Assert.That(p[22]).IsEqualTo((byte)34);
        await Assert.That(p[23]).IsEqualTo((byte)23);
        await Assert.That(p[24]).IsEqualTo((byte)45);
        await Assert.That(p[34]).IsEqualTo((byte)1); // CPU power hundreds
    }

    [Test]
    public async Task Time_fields_are_encoded()
    {
        var p = SegmentLcdProtocol.EncodeUpdate(new SegmentLcdFrame { Time = FixedTime });

        byte[] expected = [20, 26, 11, 20, 13, 45, 30, 5];
        await Assert.That(Convert.ToHexString(p, 25, 8)).IsEqualTo(Convert.ToHexString(expected));
    }

    [Test]
    [Arguments(42.999, (byte)43, (byte)0)]
    [Arguments(42.994, (byte)42, (byte)99)]
    [Arguments(0.005, (byte)0, (byte)1)]
    [Arguments(300.0, (byte)255, (byte)99)]
    [Arguments(-5.0, (byte)0, (byte)0)]
    [Arguments(double.NaN, (byte)0, (byte)0)]
    public async Task Temperature_is_rounded_and_clamped(double celsius, byte whole, byte fraction)
    {
        var p = SegmentLcdProtocol.EncodeUpdate(new SegmentLcdFrame { CpuTemperature = celsius, Time = FixedTime });

        await Assert.That(p[3]).IsEqualTo(whole);
        await Assert.That(p[4]).IsEqualTo(fraction);
    }

    [Test]
    public async Task Fahrenheit_converts_and_sets_unit_flag()
    {
        var p = SegmentLcdProtocol.EncodeUpdate(new SegmentLcdFrame
        {
            CpuTemperature = 50,
            GpuTemperature = 100,
            Unit = TemperatureUnit.Fahrenheit,
            Time = FixedTime,
        });

        await Assert.That(p[3]).IsEqualTo((byte)122);
        await Assert.That(p[5]).IsEqualTo((byte)1);
        await Assert.That(p[13]).IsEqualTo((byte)212);
        await Assert.That(p[15]).IsEqualTo((byte)1);
    }

    [Test]
    [Arguments(0.0, (byte)0, (byte)0)]
    [Arguments(99.0, (byte)0, (byte)99)]
    [Arguments(1234.0, (byte)12, (byte)34)]
    [Arguments(9999.0, (byte)99, (byte)99)]
    [Arguments(20000.0, (byte)99, (byte)99)]
    public async Task Rpm_is_split_base100(double rpm, byte high, byte low)
    {
        var p = SegmentLcdProtocol.EncodeUpdate(new SegmentLcdFrame { FanSpeed = rpm, Time = FixedTime });

        await Assert.That(p[21]).IsEqualTo(high);
        await Assert.That(p[22]).IsEqualTo(low);
    }

    [Test]
    public async Task Gpu_power_uses_three_bytes()
    {
        var p = SegmentLcdProtocol.EncodeUpdate(new SegmentLcdFrame { GpuPower = 345.67, Time = FixedTime });

        await Assert.That(p[35]).IsEqualTo((byte)3);
        await Assert.That(p[17]).IsEqualTo((byte)45);
        await Assert.That(p[18]).IsEqualTo((byte)67);
    }

    [Test]
    public async Task Padding_is_zero()
    {
        var p = SegmentLcdProtocol.EncodeUpdate(new SegmentLcdFrame
        {
            CpuTemperature = 99,
            GpuTemperature = 99,
            RamUsage = 99,
            Time = FixedTime,
        });

        await Assert.That(p.Skip(36).All(b => b == 0)).IsTrue();
    }

    [Test]
    public async Task Off_packet()
    {
        var p = SegmentLcdProtocol.EncodeOff();

        await Assert.That(p.Length).IsEqualTo(64);
        await Assert.That(p[1]).IsEqualTo((byte)0x0F);
        await Assert.That(p.Where((_, i) => i != 1).All(b => b == 0)).IsTrue();
    }

    [Test]
    public async Task Hid_report_prepends_report_id()
    {
        var r = SegmentLcdProtocol.ToHidReport(SegmentLcdProtocol.EncodeOff());

        await Assert.That(r.Length).IsEqualTo(65);
        await Assert.That(r[0]).IsEqualTo((byte)0x00);
        await Assert.That(r[2]).IsEqualTo((byte)0x0F);
    }

    [Test]
    public async Task Usage_hundredths_are_encoded_at_48_and_50()
    {
        var p = SegmentLcdProtocol.EncodeUpdate(new SegmentLcdFrame { CpuUsage = 37.25, RamUsage = 27.98, Time = FixedTime });

        await Assert.That(p[6]).IsEqualTo((byte)37);
        await Assert.That(p[48]).IsEqualTo((byte)25);
        await Assert.That(p[33]).IsEqualTo((byte)27);
        await Assert.That(p[50]).IsEqualTo((byte)98);
    }

    [Test]
    public async Task Full_usage_has_zero_fraction()
    {
        var p = SegmentLcdProtocol.EncodeUpdate(new SegmentLcdFrame { CpuUsage = 100, Time = FixedTime });

        await Assert.That(p[6]).IsEqualTo((byte)100);
        await Assert.That(p[48]).IsEqualTo((byte)0);
    }

    [Test]
    public async Task Reproduces_packet_of_original_software_except_byte_37()
    {
        // second capture of the original software (2026-11-20), t = 0.161 s (TT SEGMENT LCD Software, VM)
        const string original =
            "00010200000006000000000137000000000000000000000000141a0b14030e35051b000000050000000000000000000014004000000000000000000000000000";

        var p = SegmentLcdProtocol.EncodeUpdate(new SegmentLcdFrame
        {
            CpuUsage = 6.20,
            CpuVoltage = 1.55,
            RamUsage = 27.64,
            Time = new DateTime(2026, 11, 20, 3, 14, 53),
        });
        p[SegmentLcdProtocol.Offsets.Unknown37] = 5; // meaning unknown, not written by the encoder

        await Assert.That(Convert.ToHexStringLower(p)).IsEqualTo(original);
    }
}