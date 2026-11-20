using Microsoft.Extensions.Localization;

namespace SegLcd.Localization;

/// <summary>
/// The localizer for places without dependency injection (attribute descriptions via <c>CliTexts</c>, the help
/// builder, libraries). <c>Program</c> sets it once at start-up; the DI factory returns the same instance.
/// </summary>
public static class Texts
{
    /// <summary>The localizer of the display language; English until <c>Program</c> sets it.</summary>
    public static IStringLocalizer Current { get; set; } = PoStringLocalizer.English;
}