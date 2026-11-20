using System.Globalization;

using Karambolo.PO;

using Microsoft.Extensions.Localization;

namespace SegLcd.Localization;

/// <summary>Plural lookup, offered by the localizers of this program in addition to <see cref="IStringLocalizer"/>.</summary>
public interface IPluralStringLocalizer
{
    /// <summary>The translation of <paramref name="singular"/>/<paramref name="plural"/> for <paramref name="count"/>.</summary>
    /// <returns>The translated format, or the English <paramref name="singular"/>/<paramref name="plural"/> (count 1 / other) when there is none.</returns>
    string GetPlural(string singular, string plural, long count);
}

/// <summary>
/// Looks up messages in one GNU gettext catalog. The English text is the message id, so a missing or empty
/// translation yields the English text.
/// </summary>
/// <param name="catalog">The parsed catalog; <see langword="null"/> for English.</param>
public sealed class PoStringLocalizer(POCatalog? catalog) : IStringLocalizer, IPluralStringLocalizer
{
    /// <summary>Localizer without catalog: every message is shown in English.</summary>
    public static PoStringLocalizer English { get; } = new(null);

    /// <inheritdoc />
    public LocalizedString this[string name] => Get(name);

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var text = Get(name);
            return new LocalizedString(name, LocalizerExtensions.Format(text.Value, name, arguments), text.ResourceNotFound);
        }
    }

    /// <inheritdoc />
    public string GetPlural(string singular, string plural, long count)
    {
        var translation = catalog?.GetTranslation(new POKey(singular, plural), (int)Math.Clamp(count, int.MinValue, int.MaxValue));
        return !string.IsNullOrEmpty(translation) ? translation : count == 1 ? singular : plural;
    }

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        catalog is null
            ? []
            : catalog.OfType<POSingularEntry>()
                .Where(entry => !string.IsNullOrEmpty(entry.Translation))
                .Select(entry => new LocalizedString(entry.Key.Id, entry.Translation));

    private LocalizedString Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var translation = catalog?.GetTranslation(new POKey(name));
        return string.IsNullOrEmpty(translation)
            ? new LocalizedString(name, name, resourceNotFound: true)
            : new LocalizedString(name, translation);
    }

    /// <inheritdoc />
    public override string ToString() => catalog?.Language ?? CultureInfo.InvariantCulture.Name;
}