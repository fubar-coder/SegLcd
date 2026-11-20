using System.Globalization;

namespace SegLcd.Localization;

/// <summary>Determines the display language from the environment.</summary>
public static class DisplayLanguage
{
    /// <summary>
    /// Reads <c>LC_MESSAGES</c>, or <c>LANG</c> when <c>LC_MESSAGES</c> is unset or empty, and takes the language part
    /// before <c>_</c>, <c>.</c> or <c>@</c> (lower-cased). <c>LC_ALL</c> and <c>LANGUAGE</c> are deliberately ignored.
    /// </summary>
    /// <param name="getEnvironmentVariable">Environment lookup.</param>
    /// <param name="isAvailable">Whether a catalog exists for a language; defaults to the embedded catalogs.</param>
    /// <returns>The language (e.g. <c>de</c>), or <see langword="null"/> for English (also for <c>C</c>, <c>POSIX</c>,
    /// unset and unsupported values).</returns>
    public static string? Resolve(Func<string, string?> getEnvironmentVariable, Func<string, bool>? isAvailable = null)
    {
        var value = getEnvironmentVariable("LC_MESSAGES");
        if (string.IsNullOrEmpty(value))
        {
            value = getEnvironmentVariable("LANG");
        }

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var end = value.AsSpan().IndexOfAny('_', '.', '@');
        var language = (end < 0 ? value : value[..end]).ToLowerInvariant();
        return language.Length > 0 && (isAvailable ?? PoStringLocalizerFactory.HasEmbeddedCatalog)(language) ? language : null;
    }

    /// <summary>
    /// Sets <see cref="CultureInfo.CurrentUICulture"/> (and the default for new threads) to <paramref name="language"/>,
    /// so System.CommandLine uses its own translations. <see cref="CultureInfo.CurrentCulture"/> stays invariant, so
    /// number and date formatting do not change.
    /// </summary>
    public static void ApplyToUserInterfaceCulture(string? language)
    {
        if (language is null)
        {
            return;
        }

        var culture = new CultureInfo(language);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}