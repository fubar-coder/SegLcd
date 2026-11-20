using DotMake.CommandLine;

using SegLcd.Localization;
using SegLcd.Tool;

// Display language from LC_MESSAGES / LANG; must be set before DotMake builds the parser (it reads the descriptions).
var language = DisplayLanguage.Resolve(Environment.GetEnvironmentVariable);
DisplayLanguage.ApplyToUserInterfaceCulture(language);
var localization = new PoStringLocalizerFactory(language);
Texts.Current = localization.Localizer;
if (localization.LoadError is { } error)
    Console.Error.WriteLine($"Warning: catalog {error.Catalog} unreadable, using English: {error.Reason}");

var parser = Cli.GetParser<RootCliCommand>();
LocalizedHelpBuilder.Install(parser.RootCommand, Texts.Current);
var exitCode = await parser.RunAsync(args);
HidApi.Hid.Exit(); // release hidapi resources
return exitCode;

namespace SegLcd.Tool
{
    [CliCommand(
        Name = "seglcd",
        ShortFormAutoGenerate = CliNameAutoGenerate.None,
        Description = nameof(CliTexts.RootDescription),
        Children = [
            typeof(ListCommand),
            typeof(SendCommand),
            typeof(LiveCommand),
            typeof(ProbeCommand),
            typeof(SweepCommand),
            typeof(RawCommand),
            typeof(OffCommand),
            typeof(ListenCommand),
            typeof(HoldCommand),
            typeof(SensorsCommand),
            typeof(DecodeCommand),
        ])]
    public sealed class RootCliCommand
    {
        public void Run(CliContext context) => context.ShowHelp();
    }
}