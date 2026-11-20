using Microsoft.Extensions.Localization;

namespace SegLcd.Localization;

/// <summary>
/// <see cref="IStringLocalizer{T}"/> over the factory's localizer. Replaces M.E's <c>StringLocalizer&lt;T&gt;</c>, which
/// would hide <see cref="IPluralStringLocalizer"/>.
/// </summary>
/// <typeparam name="T">The type the localizer is for.</typeparam>
public sealed class TypedStringLocalizer<T>(IStringLocalizerFactory factory) : IStringLocalizer<T>, IPluralStringLocalizer
{
    private readonly IStringLocalizer _inner = factory.Create(typeof(T));

    /// <inheritdoc />
    public LocalizedString this[string name] => _inner[name];

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments] => _inner[name, arguments];

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => _inner.GetAllStrings(includeParentCultures);

    /// <inheritdoc />
    public string GetPlural(string singular, string plural, long count) =>
        _inner is IPluralStringLocalizer plurals ? plurals.GetPlural(singular, plural, count) : count == 1 ? singular : plural;
}