using System.Globalization;

using SegLcd.Localization;

namespace SegLcd.Tool;

internal static class Hex
{
    /// <summary>Formats bytes as "00 01 02 …", trimming trailing zero bytes (shown as "…").</summary>
    public static string Format(ReadOnlySpan<byte> data, bool trimTrailingZeros = true)
    {
        var length = data.Length;
        if (trimTrailingZeros)
        {
            while (length > 0 && data[length - 1] == 0)
                length--;
        }

        var text = Convert.ToHexString(data[..length]).ToLowerInvariant();
        var parts = Enumerable.Range(0, length).Select(i => text.Substring(i * 2, 2));
        var result = string.Join(' ', parts);
        return length < data.Length ? $"{result} … ({data.Length} Bytes)" : result;
    }

    /// <summary>Parses "00 01 02", "000102", "00,01,02" or "0x00 0x01" into bytes.</summary>
    public static byte[] Parse(IEnumerable<string> tokens)
    {
        var joined = string.Concat(tokens.SelectMany(t => t.Split([' ', ',', ':', '-'], StringSplitOptions.RemoveEmptyEntries))
            .Select(t => t.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? t[2..].PadLeft(2, '0') : t));
        if (joined.Length % 2 != 0)
            throw new FormatException(Texts.Current.T("Odd number of hex digits."));
        return Convert.FromHexString(joined);
    }

    public static string Byte(byte b) => b.ToString("x2", CultureInfo.InvariantCulture);
}