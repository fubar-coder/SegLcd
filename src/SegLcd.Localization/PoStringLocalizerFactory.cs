using Karambolo.PO;

using Microsoft.Extensions.Localization;

namespace SegLcd.Localization;

/// <summary>
/// Creates the localizer for the display language from the catalog <c>&lt;language&gt;.po</c> embedded in the program.
/// There is only one catalog domain, so every <see cref="Create(Type)"/> returns the same localizer.
/// </summary>
public sealed class PoStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly PoStringLocalizer _localizer;

    /// <summary>Parses the catalog for <paramref name="language"/>.</summary>
    /// <param name="language">Language from <see cref="DisplayLanguage.Resolve"/>; <see langword="null"/> for English.</param>
    /// <param name="openCatalog">Opens the catalog of a language, or returns null; defaults to the embedded catalogs.</param>
    /// <remarks>
    /// A catalog that cannot be parsed does not fail: English is used and <see cref="LoadError"/> tells why.
    /// </remarks>
    public PoStringLocalizerFactory(string? language, Func<string, Stream?>? openCatalog = null)
    {
        _localizer = PoStringLocalizer.English;
        if (language is null)
        {
            return;
        }

        using var stream = (openCatalog ?? OpenEmbeddedCatalog)(language);
        if (stream is null)
        {
            return;
        }

        var result = new POParser(new POParserSettings { SkipComments = true, SkipInfoHeaders = true }).Parse(stream);
        if (!result.Success)
        {
            LoadError = (CatalogName(language), string.Join("; ", result.Diagnostics.Select(d => d.ToString())));
            return;
        }

        _localizer = new PoStringLocalizer(result.Catalog);
        Language = language;
    }

    /// <summary>Factory for English (no catalog).</summary>
    public static PoStringLocalizerFactory English { get; } = new(null);

    /// <summary>Language whose catalog is in use; <see langword="null"/> for English.</summary>
    public string? Language { get; }

    /// <summary>The catalog that could not be parsed and why; <see langword="null"/> when it was fine.</summary>
    public (string Catalog, string Reason)? LoadError { get; }

    /// <summary>The localizer for every resource type.</summary>
    public IStringLocalizer Localizer => _localizer;

    /// <summary>Whether a catalog for <paramref name="language"/> is embedded in the program.</summary>
    public static bool HasEmbeddedCatalog(string language) =>
        typeof(PoStringLocalizerFactory).Assembly.GetManifestResourceInfo(CatalogName(language)) is not null;

    /// <summary>Opens the embedded catalog of <paramref name="language"/>, or returns <see langword="null"/>.</summary>
    public static Stream? OpenEmbeddedCatalog(string language) =>
        typeof(PoStringLocalizerFactory).Assembly.GetManifestResourceStream(CatalogName(language));

    /// <summary>Resource name of the catalog of <paramref name="language"/>.</summary>
    public static string CatalogName(string language) => language + ".po";

    /// <inheritdoc />
    public IStringLocalizer Create(Type resourceSource) => _localizer;

    /// <inheritdoc />
    public IStringLocalizer Create(string baseName, string location) => _localizer;
}