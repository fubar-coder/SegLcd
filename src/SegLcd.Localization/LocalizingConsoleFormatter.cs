
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace SegLcd.Localization;

/// <summary>
/// Console formatter that shows log messages in the display language. It translates the English message template
/// (<c>{OriginalFormat}</c> of a <c>[LoggerMessage]</c> event) and substitutes the named placeholders with the event's
/// values, then hands the entry to <paramref name="inner"/> (the simple formatter), so the layout stays the same.
/// Other providers (journal, file) keep receiving the English message.
/// </summary>
/// <param name="texts">Localizer of the display language.</param>
/// <param name="inner">The formatter that writes the line; resolved on first use.</param>
public sealed class LocalizingConsoleFormatter(IStringLocalizer texts, Func<ConsoleFormatter> inner) : ConsoleFormatter(FormatterName)
{
    /// <summary>Name to select this formatter in <see cref="ConsoleLoggerOptions.FormatterName"/>.</summary>
    public const string FormatterName = "localized";

    private readonly Lazy<ConsoleFormatter> _inner = new(inner);

    /// <inheritdoc />
    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        if (Translate(logEntry.State) is { } message)
        {
            var translated = new LogEntry<TState>(
                logEntry.LogLevel,
                logEntry.Category,
                logEntry.EventId,
                logEntry.State,
                logEntry.Exception,
                (_, _) => message);
            _inner.Value.Write(in translated, scopeProvider, textWriter);
        }
        else
        {
            _inner.Value.Write(in logEntry, scopeProvider, textWriter);
        }
    }

    /// <summary>
    /// The translated message of <paramref name="state"/>, or <see langword="null"/> when the state has no template or
    /// the template has no translation (the English message is used unchanged then).
    /// </summary>
    public string? Translate<TState>(TState state)
    {
        if (state is not IReadOnlyList<KeyValuePair<string, object?>> values)
        {
            return null;
        }

        string? template = null;
        foreach (var (key, value) in values)
        {
            if (key == MessageTemplate.OriginalFormatKey)
            {
                template = value as string;
                break;
            }
        }

        if (string.IsNullOrEmpty(template))
        {
            return null;
        }

        var translation = texts[template];
        return translation.ResourceNotFound ? null : MessageTemplate.Format(translation.Value, values);
    }
}