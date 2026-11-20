using System.Collections;
using System.Globalization;
using System.Text;

namespace SegLcd.Localization;

/// <summary>
/// Formats message templates with named placeholders (<c>{Path}</c>, <c>{Count,5}</c>, <c>{Value:N2}</c>) like
/// Microsoft.Extensions.Logging does: values are formatted invariantly, <see langword="null"/> as <c>(null)</c>, and
/// <c>{{</c>/<c>}}</c> are literal braces. Placeholders without a value are left as they are.
/// </summary>
public static class MessageTemplate
{
    /// <summary>Key of the template in the state of a <c>[LoggerMessage]</c> event.</summary>
    public const string OriginalFormatKey = "{OriginalFormat}";

    /// <summary>Substitutes the named placeholders of <paramref name="template"/> with <paramref name="values"/>.</summary>
    public static string Format(string template, IEnumerable<KeyValuePair<string, object?>> values)
    {
        var lookup = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            lookup.TryAdd(key, value);
        }

        var builder = new StringBuilder(template.Length + 32);
        var i = 0;
        while (i < template.Length)
        {
            var c = template[i];
            if ((c == '{' || c == '}') && i + 1 < template.Length && template[i + 1] == c)
            {
                builder.Append(c);
                i += 2;
                continue;
            }

            if (c == '{' && template.IndexOf('}', i + 1) is var close and > 0)
            {
                var hole = template.Substring(i + 1, close - i - 1);
                if (TryFormatHole(hole, lookup, out var text))
                {
                    builder.Append(text);
                    i = close + 1;
                    continue;
                }
            }

            builder.Append(c);
            i++;
        }

        return builder.ToString();
    }

    /// <summary>The placeholder names of <paramref name="template"/>, in order of appearance.</summary>
    public static IReadOnlyList<string> Placeholders(string template)
    {
        var names = new List<string>();
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] == '{' && i + 1 < template.Length && template[i + 1] == '{')
            {
                i++;
                continue;
            }

            if (template[i] == '{' && template.IndexOf('}', i + 1) is var close and > 0)
            {
                names.Add(Split(template.Substring(i + 1, close - i - 1)).Name);
                i = close;
            }
        }

        return names;
    }

    private static bool TryFormatHole(string hole, Dictionary<string, object?> values, out string text)
    {
        var (name, alignment, format) = Split(hole);
        if (name.Length == 0 || !values.TryGetValue(name, out var value))
        {
            text = string.Empty;
            return false;
        }

        text = FormatValue(value, format);
        if (alignment is not null && int.TryParse(alignment, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var width))
        {
            text = width < 0 ? text.PadRight(-width) : text.PadLeft(width);
        }

        return true;
    }

    private static (string Name, string? Alignment, string? Format) Split(string hole)
    {
        string? format = null;
        var colon = hole.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            format = hole[(colon + 1)..];
            hole = hole[..colon];
        }

        string? alignment = null;
        var comma = hole.IndexOf(',', StringComparison.Ordinal);
        if (comma >= 0)
        {
            alignment = hole[(comma + 1)..].Trim();
            hole = hole[..comma];
        }

        return (hole.Trim(), alignment, format);
    }

    private static string FormatValue(object? value, string? format) => value switch
    {
        null => "(null)",
        string text => text,
        IFormattable formattable => formattable.ToString(format, CultureInfo.InvariantCulture),
        IEnumerable sequence => string.Join(", ", sequence.Cast<object?>().Select(item => FormatValue(item, format))),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };
}