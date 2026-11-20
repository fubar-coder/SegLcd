using System.CodeDom.Compiler;

using SegLcd.Localization;

namespace SegLcd.Tool;

/// <summary>
/// Descriptions of the commands, options and arguments, in the display language. Attributes reference them with
/// <c>Description = nameof(CliTexts.X)</c>.
/// </summary>
/// <remarks>
/// The <see cref="GeneratedCodeAttribute"/> is load-bearing: DotMake.CommandLine treats a class carrying
/// <c>StronglyTypedResourceBuilder</c>'s attribute as resource class and emits <c>command.Description = CliTexts.X</c>
/// (read at runtime) instead of the literal name. The class must be hand-written source, because generator output is
/// not visible to DotMake's generator.
/// </remarks>
[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
public static class CliTexts
{
    // --- root and shared ---------------------------------------------------------------------------------------
    public static string RootDescription => Texts.Current.T("Test and analysis tool for the Thermaltake segment LCD (e.g. LB360-S ARGB Sync).");
    public static string DeviceOption => Texts.Current.T("Device path (e.g. /dev/hidraw5). Default: first segment LCD found.");
    public static string DryRunOption => Texts.Current.T("Send nothing, only print the (first) packet as hex.");
    public static string VerboseOption => Texts.Current.T("Print every packet sent.");
    public static string DurationOption => Texts.Current.T("Stop after N seconds.");
    public static string IntervalOption => Texts.Current.T("Send interval in ms.");
    public static string FahrenheitOption => Texts.Current.T("Send temperatures in °F (not suitable for the LB360-S).");

    // --- list ----------------------------------------------------------------------------------------------------
    public static string ListDescription => Texts.Current.T("Show segment LCDs found (or all HID devices with --all) with their descriptors.");
    public static string ListAllOption => Texts.Current.T("Show all HID devices, not only known segment LCDs.");

    // --- send ----------------------------------------------------------------------------------------------------
    public static string SendDescription => Texts.Current.T("Send fixed values to the display (keep-alive every 250 ms).");
    public static string CpuTempOption => Texts.Current.T("CPU temperature in °C.");
    public static string CpuUsageOption => Texts.Current.T("CPU usage in %.");
    public static string CpuPowerOption => Texts.Current.T("CPU power in W.");
    public static string CpuClockOption => Texts.Current.T("CPU clock in MHz.");
    public static string CpuVoltageOption => Texts.Current.T("CPU voltage in V.");
    public static string GpuTempOption => Texts.Current.T("GPU temperature in °C.");
    public static string GpuUsageOption => Texts.Current.T("GPU usage in %.");
    public static string GpuPowerOption => Texts.Current.T("GPU power in W.");
    public static string GpuClockOption => Texts.Current.T("GPU clock in MHz.");
    public static string FanOption => Texts.Current.T("Fan speed in RPM.");
    public static string PumpOption => Texts.Current.T("Pump speed in RPM.");
    public static string RamOption => Texts.Current.T("RAM usage in %.");
    public static string TimeOption => Texts.Current.T("Time of day (ISO 8601), default: now.");
    public static string OffAtEndOption => Texts.Current.T("Send the off packet (0x0F) when finished.");

    // --- live ----------------------------------------------------------------------------------------------------
    public static string LiveDescription => Texts.Current.T("Send live sensor values to the display.");
    public static string CpuTempSensorOption => Texts.Current.T("CPU temperature sensor. Default: k10temp:Tctl, zenpower:Tdie, zenpower:Tctl, coretemp:Package id 0.");
    public static string CpuPowerSensorOption => Texts.Current.T("CPU power sensor. Default: rapl, otherwise zenpower:SVI2_P_Core+zenpower:SVI2_P_SoC.");
    public static string CpuVoltageSensorOption => Texts.Current.T("CPU voltage sensor. Default: zenpower:SVI2_Core.");
    public static string CpuClockModeOption => Texts.Current.T("CPU clock from all cores: Avg (average), Max, Min or First (core 0).");
    public static string GpuTempSensorOption => Texts.Current.T("GPU temperature sensor. Default: amdgpu:edge, nvml:temperature, xe:pkg.");
    public static string GpuUsageSensorOption => Texts.Current.T("GPU usage source. Default: drm:busy, nvml:usage.");
    public static string GpuPowerSensorOption => Texts.Current.T("GPU power sensor. Default: amdgpu:PPT, nvml:power, rapl:uncore.");
    public static string GpuClockSensorOption => Texts.Current.T("GPU clock sensor. Default: amdgpu:sclk, nvml:clock, drm:freq.");
    public static string FanSensorOption => Texts.Current.T("Fan sensor (e.g. it8686_31050607:fan1). See 'seglcd sensors'.");
    public static string PumpSensorOption => Texts.Current.T("Pump sensor (e.g. it8686_31050607:fan5).");
    public static string SampleIntervalOption => Texts.Current.T("Read the sensors every N ms.");
    public static string ShowOption => Texts.Current.T("Print the values read.");

    // --- probe ---------------------------------------------------------------------------------------------------
    public static string ProbeDescription => Texts.Current.T("Set single bytes interactively to find out which field the display shows.");
    public static string ProbeValueOption => Texts.Current.T("Value written into the byte under test.");
    public static string ProbeValuesOption => Texts.Current.T("Sequence of values per byte instead of --value, e.g. 0,1,50,99,128,255.");
    public static string ProbeValueSecondsOption => Texts.Current.T("Seconds per value of the --values sequence.");
    public static string ProbeOffsetsOption => Texts.Current.T("Only test these offsets (comma-separated, e.g. 3,13,21 or ranges 36-63). Default: 3–63.");
    public static string ProbeAutoOption => Texts.Current.T("Advance automatically every N seconds instead of waiting for Enter.");
    public static string ProbeNoHeaderOption => Texts.Current.T("Leave bytes 1/2 (command) free instead of setting them to 01 02.");

    // --- sweep ---------------------------------------------------------------------------------------------------
    public static string SweepDescription => Texts.Current.T("Count the value of one byte up step by step (e.g. for bar graphs).");
    public static string SweepOffsetArgument => Texts.Current.T("Byte offset in the 64-byte packet (3–63).");
    public static string SweepFromOption => Texts.Current.T("Start value.");
    public static string SweepToOption => Texts.Current.T("End value.");
    public static string SweepStepOption => Texts.Current.T("Step size.");
    public static string SweepStepMsOption => Texts.Current.T("Milliseconds per step.");
    public static string SweepSetOption => Texts.Current.T("Further fixed bytes as offset=value (e.g. 21=12,22=34).");

    // --- raw / off / listen / hold ---------------------------------------------------------------------------------
    public static string RawDescription => Texts.Current.T("Send an arbitrary packet (hex, max. 64 bytes, without report ID) periodically.");
    public static string RawBytesArgument => Texts.Current.T("Hex bytes, e.g. \"00 01 02 2a 32\". The rest is filled with 00.");
    public static string RawOnceOption => Texts.Current.T("Send only once instead of periodically.");
    public static string OffDescription => Texts.Current.T("Switch the display off (packet 0x0F).");
    public static string ListenDescription => Texts.Current.T("Read the device's input reports (meaning unknown so far).");
    public static string ListenKeepAliveOption => Texts.Current.T("Send update packets with zero values in parallel (in case the device only answers then).");
    public static string HoldDescription => Texts.Current.T("Timeout test: send for N seconds, then stop but keep the device open for M more seconds.");
    public static string HoldSendOption => Texts.Current.T("Send update packets for this long (seconds).");
    public static string HoldHoldOption => Texts.Current.T("Then keep the device open without sending for this long (seconds).");
    public static string HoldCpuTempOption => Texts.Current.T("CPU temperature to show.");
    public static string HoldGpuTempOption => Texts.Current.T("GPU temperature to show.");

    // --- sensors / decode -------------------------------------------------------------------------------------------
    public static string SensorsDescription => Texts.Current.T("List all available sensor sources with current values (for 'live' and seglcdd).");
    public static string DecodeDescription => Texts.Current.T("Decode captured packets (hex, one line per packet, e.g. tshark output).");
    public static string DecodeFileArgument => Texts.Current.T("File with hex lines; '-' or empty = stdin.");
    public static string DecodeDiffOption => Texts.Current.T("Only show bytes that changed compared to the previous packet.");
    public static string DecodeStripReportIdOption => Texts.Current.T("Remove a leading report ID byte (0x00) from 65-byte lines.");
}