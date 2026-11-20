using System.CommandLine;
using System.CommandLine.Help;

using DotMake.CommandLine;
using DotMake.CommandLine.Help;

using Microsoft.Extensions.Localization;

using SegLcd.Localization;

namespace SegLcd.Tool;

/// <summary>
/// DotMake's help output with the labels that System.CommandLine's own translations leave English: <c>[command]</c>
/// and <c>[options]</c> in the usage line, <c>[required]</c> and <c>[default: …]</c> in the descriptions. Section
/// titles and parser errors already come translated from System.CommandLine (via <c>CurrentUICulture</c>).
/// </summary>
/// <remarks>
/// DotMake's <c>LocalizationResources</c> is internal and the label lookups are private, so the public virtual
/// sections are overridden: the usage section is rendered into a buffer and its labels replaced (it has no styling in
/// the default theme); argument and option rows are collected with <see cref="CliHelpBuilder.GetTwoColumnRow"/>, the
/// labels replaced, and written with the styled <see cref="CliHelpBuilder.WriteColumns"/>.
/// </remarks>
/// <param name="theme">Theme of the replaced builder.</param>
/// <param name="texts">Localizer of the display language.</param>
public sealed class LocalizedHelpBuilder(CliTheme theme, IStringLocalizer texts) : CliHelpBuilder(theme)
{
    // The English labels exactly as DotMake writes them. The T(...) calls below repeat them as literals for xgettext.
    private const string CommandLabel = "[command]";
    private const string OptionsLabel = "[options]";
    private const string RequiredLabel = "[required]";
    private const string DefaultLabelPrefix = "[default: ";

    /// <summary>
    /// Translates the help option's description and replaces the help builder of <paramref name="root"/>'s help option,
    /// which DotMake also uses for <see cref="CliContext.ShowHelp"/>.
    /// </summary>
    public static void Install(RootCommand root, IStringLocalizer texts)
    {
        if (root.Options.OfType<HelpOption>().FirstOrDefault() is not { } help)
        {
            return;
        }

        help.Description = texts.T("Show help and usage information");
        if (help.Action is CustomHelpAction action)
        {
            action.Builder = new LocalizedHelpBuilder((action.Builder as CliHelpBuilder)?.Theme ?? CliTheme.Default, texts);
        }
    }

    /// <inheritdoc />
    public override bool WriteCommandUsageSection(HelpContext helpContext)
    {
        var buffer = new StringWriter();
        var written = base.WriteCommandUsageSection(new HelpContext(this, helpContext.Command, buffer, helpContext.ParseResult));
        helpContext.Output.Write(WithoutParentArguments(buffer.ToString(), helpContext.Command)
            // translators: placeholder for a sub-command in the usage line of the help
            .Replace(" " + CommandLabel, " " + texts.T("[command]"), StringComparison.Ordinal)
            // translators: placeholder for options in the usage line of the help
            .Replace(" " + OptionsLabel, " " + texts.T("[options]"), StringComparison.Ordinal));
        return written;
    }

    /// <inheritdoc />
    public override bool WriteCommandArgumentsSection(HelpContext helpContext)
    {
        // Only the command's own arguments (seglcd's root has none).
        var rows = helpContext.Command.Arguments
            .Where(argument => !argument.Hidden)
            .Select(argument => Translate(GetTwoColumnRow(argument, helpContext)))
            .ToList();
        return WriteSection(helpContext, rows, base.WriteCommandArgumentsSection);
    }

    /// <inheritdoc />
    public override bool WriteCommandOptionsSection(HelpContext helpContext)
    {
        // Same selection as DotMake: the command's options, then the recursive options of its parents.
        var seen = new HashSet<Option>();
        var rows = new List<TwoColumnHelpRow>();
        foreach (var option in helpContext.Command.Options.Where(option => !option.Hidden && seen.Add(option)))
        {
            rows.Add(Translate(GetTwoColumnRow(option, helpContext)));
        }

        foreach (var parent in CommandAndParents(helpContext.Command).Skip(1))
        {
            foreach (var option in parent.Options.Where(option => option.Recursive && !option.Hidden && seen.Add(option)))
            {
                rows.Add(Translate(GetTwoColumnRow(option, helpContext)));
            }
        }

        return WriteSection(helpContext, rows, base.WriteCommandOptionsSection);
    }

    /// <summary>Removes the parents' arguments from the usage line (none in seglcd; kept for future sub-commands).</summary>
    private static string WithoutParentArguments(string usage, Command command)
    {
        foreach (var parent in CommandAndParents(command).Skip(1))
        {
            foreach (var argument in parent.Arguments.Where(argument => !argument.Hidden))
            {
                var name = $"<{argument.HelpName ?? argument.Name}>";
                var token = argument.Arity.MinimumNumberOfValues == 0 ? $"[{name}]" : name;
                usage = usage.Replace($"{parent.Name} {token} ", $"{parent.Name} ", StringComparison.Ordinal);
            }
        }

        return usage;
    }

    private static IEnumerable<Command> CommandAndParents(Command command)
    {
        for (Command? current = command; current is not null; current = current.Parents.OfType<Command>().FirstOrDefault())
        {
            yield return current;
        }
    }

    /// <summary>Writes the heading as the base section does (rendered into a buffer), then the rows.</summary>
    private bool WriteSection(HelpContext helpContext, List<TwoColumnHelpRow> rows, Func<HelpContext, bool> baseSection)
    {
        if (rows.Count == 0)
        {
            return false;
        }

        var buffer = new StringWriter();
        baseSection(new HelpContext(this, helpContext.Command, buffer, helpContext.ParseResult));
        var heading = buffer.ToString().Split('\n', 2)[0].TrimEnd('\r');
        helpContext.Output.SetStyle(Theme.HeadingStyle ?? Theme.DefaultStyle);
        helpContext.Output.WriteLine(heading);
        helpContext.Output.SetStyle(Theme.DefaultStyle);
        WriteColumns(rows, helpContext);
        return true;
    }

    private TwoColumnHelpRow Translate(TwoColumnHelpRow row) =>
        new(
            row.FirstColumnText,
            row.SecondColumnText
                // translators: marks a required argument in the help
                .Replace(RequiredLabel, texts.T("[required]"), StringComparison.Ordinal)
                // translators: label of a default value in the help, as in "[default: False]"
                .Replace(DefaultLabelPrefix, "[" + texts.T("default") + ": ", StringComparison.Ordinal));
}