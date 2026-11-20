using Microsoft.Extensions.Localization;

using SegLcd.Localization;

namespace SegLcd.Tool;

/// <summary>Translated names of the packet bytes (for probe and decode).</summary>
internal static class FieldTexts
{
    private static IStringLocalizer L => Texts.Current;

    public static string Name(int offset) => offset switch
    {
        0 => L.T("reserved (always 00)"),
        1 => L.T("command"),
        2 => L.T("sub-command"),
        3 => L.T("CPU temperature (integer part)"),
        4 => L.T("CPU temperature (hundredths)"),
        5 => L.T("CPU temperature unit (0=°C, 1=°F)"),
        6 => L.T("CPU usage % (integer part)"),
        7 => L.T("CPU power W mod 100"),
        8 => L.T("CPU power (hundredths)"),
        9 => L.T("CPU clock MHz / 100"),
        10 => L.T("CPU clock MHz mod 100"),
        11 => L.T("CPU voltage (integer part)"),
        12 => L.T("CPU voltage (hundredths)"),
        13 => L.T("GPU temperature (integer part)"),
        14 => L.T("GPU temperature (hundredths)"),
        15 => L.T("GPU temperature unit (0=°C, 1=°F)"),
        16 => L.T("GPU usage %"),
        17 => L.T("GPU power W mod 100"),
        18 => L.T("GPU power (hundredths)"),
        19 => L.T("GPU clock MHz / 100"),
        20 => L.T("GPU clock MHz mod 100"),
        21 => L.T("fan RPM / 100"),
        22 => L.T("fan RPM mod 100"),
        23 => L.T("pump RPM / 100"),
        24 => L.T("pump RPM mod 100"),
        25 => L.T("year / 100"),
        26 => L.T("year mod 100"),
        27 => L.T("month"),
        28 => L.T("day"),
        29 => L.T("hour"),
        30 => L.T("minute"),
        31 => L.T("second"),
        32 => L.T("day of week (0=Sunday)"),
        33 => L.T("RAM usage % (integer part)"),
        34 => L.T("CPU power W / 100"),
        35 => L.T("GPU power W / 100"),
        37 => L.T("unknown (original LB360-S software: always 5)"),
        48 => L.T("CPU usage (hundredths)"),
        50 => L.T("RAM usage (hundredths)"),
        _ => L.T("unused"),
    };
}