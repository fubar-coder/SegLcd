using System.Globalization;

using Microsoft.Extensions.Localization;

namespace SegLcd.Localization;

/// <summary>
/// The way call sites translate. <c>xgettext</c> extracts the message ids of <c>T</c> and <c>Tn</c> calls
/// (<c>-kT -kTn:1,2</c>), but not of the indexer or of interpolated strings: always pass a string literal.
/// </summary>
public static class LocalizerExtensions
{
    /// <summary>Translates <paramref name="message"/>; braces are taken literally.</summary>
    public static string T(this IStringLocalizer texts, string message) => texts[message].Value;

    /// <summary>Translates the composite format <paramref name="format"/> (<c>{0}</c>, …) and formats it invariantly.</summary>
    public static string T(this IStringLocalizer texts, string format, params object?[] arguments) =>
        Format(texts[format].Value, format, arguments);

    /// <summary>Translates a message with a plural form and formats it invariantly.</summary>
    /// <param name="texts">Localizer.</param>
    /// <param name="singular">English singular, also the message id.</param>
    /// <param name="plural">English plural.</param>
    /// <param name="count">Selects the plural form.</param>
    /// <param name="arguments">Format arguments; usually include <paramref name="count"/>.</param>
    public static string Tn(this IStringLocalizer texts, string singular, string plural, long count, params object?[] arguments)
    {
        var format = texts is IPluralStringLocalizer plurals
            ? plurals.GetPlural(singular, plural, count)
            : count == 1 ? singular : plural;
        return Format(format, count == 1 ? singular : plural, arguments);
    }

    /// <summary>
    /// Translates a message template with named placeholders (<c>{Path}</c>), as used by <c>[LoggerMessage]</c>, and
    /// substitutes <paramref name="values"/>. Not extracted by <c>xgettext</c>; the catalog test covers the templates.
    /// </summary>
    public static string TTemplate(this IStringLocalizer texts, string template, IEnumerable<KeyValuePair<string, object?>> values) =>
        MessageTemplate.Format(texts[template].Value, values);

    /// <summary>Formats <paramref name="format"/>; a broken translation falls back to the English <paramref name="fallback"/>.</summary>
    internal static string Format(string format, string fallback, object?[] arguments)
    {
        if (arguments.Length == 0)
        {
            return format;
        }

        try
        {
            return string.Format(CultureInfo.InvariantCulture, format, arguments);
        }
        catch (FormatException) when (!ReferenceEquals(format, fallback))
        {
            return string.Format(CultureInfo.InvariantCulture, fallback, arguments);
        }
    }
}