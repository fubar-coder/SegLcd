using System.Diagnostics;
using System.Reflection;

using DotMake.CommandLine;

using Karambolo.PO;

using Microsoft.Extensions.Logging;

using SegLcd.Daemon;
using SegLcd.Localization;
using SegLcd.Tool;

// The catalog checks inspect the application by reflection; tests run on the JIT, never trimmed or AOT-compiled.
#pragma warning disable IL2026, IL2070, IL2075

namespace SegLcd.Tests;

/// <summary>Checks the message catalogs in <c>po/</c> against the code (no untranslated, missing or malformed entries).</summary>
public sealed class CatalogTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string TemplateFile = Path.Combine(RepositoryRoot, "po", "seglcd.pot");
    private static readonly string GermanFile = Path.Combine(RepositoryRoot, "po", "de.po");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SegLcd.slnx")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static List<IPOEntry> Entries(string file) => [.. (IEnumerable<IPOEntry>)Parse(file)];

    private static POCatalog Parse(string file)
    {
        using var stream = File.OpenRead(file);
        var result = new POParser(new POParserSettings()).Parse(stream);
        if (!result.Success)
            throw new InvalidDataException(string.Join("; ", result.Diagnostics.Select(d => d.ToString())));
        return result.Catalog;
    }

    /// <summary>All <c>[LoggerMessage]</c> templates of seglcdd.</summary>
    private static IReadOnlyCollection<string> LogTemplates() =>
        [.. typeof(DisplayController).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(method => method.GetCustomAttribute<LoggerMessageAttribute>()?.Message)
            .OfType<string>()
            .Where(message => message.Length > 0)
            .Distinct()];

    private static bool IsInstalled(string tool) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => File.Exists(Path.Combine(directory, tool)));

    private static (int ExitCode, string Output) RunTool(string tool, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(tool) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        startInfo.Environment["LC_ALL"] = "C";
        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    [Test]
    public async Task Embedded_catalog_matches_the_file()
    {
        await using var embedded = PoStringLocalizerFactory.OpenEmbeddedCatalog("de");
        await Assert.That(embedded).IsNotNull();
        using var reader = new StreamReader(embedded!);
        await Assert.That(await reader.ReadToEndAsync()).IsEqualTo(await File.ReadAllTextAsync(GermanFile));
    }

    [Test]
    public async Task Catalogs_parse()
    {
        await Assert.That(Entries(TemplateFile).Count).IsGreaterThan(0);
        var german = Parse(GermanFile);
        await Assert.That(german.Language).IsEqualTo("de");
        await Assert.That(german.PluralFormCount).IsEqualTo(2);
    }

    [Test]
    public async Task Every_log_template_is_in_the_template_and_translated()
    {
        var template = Parse(TemplateFile);
        var german = Parse(GermanFile);
        foreach (var message in LogTemplates())
        {
            await Assert.That(template.Contains(new POKey(message))).IsTrue().Because($"missing in the .pot (run po/extract.sh): {message}");
            await Assert.That(german.GetTranslation(new POKey(message))).IsNotNullOrEmpty().Because($"not translated in de.po: {message}");
        }
    }

    [Test]
    public async Task Every_template_entry_is_translated_in_german()
    {
        var template = Parse(TemplateFile);
        var german = Parse(GermanFile);
        foreach (var entry in Entries(TemplateFile))
        {
            await Assert.That(german.TryGetValue(entry.Key, out var translation)).IsTrue().Because($"missing in de.po (run po/extract.sh): {entry.Key.Id}");
            foreach (var form in translation!)
                await Assert.That(form).IsNotNullOrEmpty().Because($"not translated in de.po: {entry.Key.Id}");
            var flags = translation!.Comments?.OfType<POFlagsComment>().SelectMany(c => c.Flags) ?? [];
            await Assert.That(flags.Contains("fuzzy")).IsFalse().Because($"fuzzy in de.po: {entry.Key.Id}");
        }

        foreach (IPOEntry entry in german)
            await Assert.That(template.Contains(entry.Key)).IsTrue().Because($"not in the .pot any more: {entry.Key.Id}");
    }

    [Test]
    public async Task Placeholders_are_kept()
    {
        foreach (var entry in Entries(GermanFile))
        {
            var expected = string.Join(",", MessageTemplate.Placeholders(entry.Key.Id).Order(StringComparer.Ordinal));
            foreach (var form in entry)
                await Assert.That(string.Join(",", MessageTemplate.Placeholders(form).Order(StringComparer.Ordinal))).IsEqualTo(expected).Because(entry.Key.Id);
        }
    }

    [Test]
    public async Task Msgfmt_accepts_the_german_catalog()
    {
        Skip.Unless(IsInstalled("msgfmt"), "msgfmt (GNU gettext) is not installed.");
        var (exitCode, output) = RunTool("msgfmt", "--check", "--output-file=/dev/null", GermanFile);
        await Assert.That(exitCode).IsEqualTo(0).Because(output);
    }

    [Test]
    public async Task Template_is_up_to_date()
    {
        Skip.Unless(IsInstalled("xgettext"), "xgettext (GNU gettext) is not installed.");
        var temp = Directory.CreateTempSubdirectory("seglcd-pot-");
        try
        {
            var sources = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                            && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
            var output = Path.Combine(temp.FullName, "code.pot");
            var (exitCode, messages) = RunTool("xgettext", ["--language=C#", "--from-code=UTF-8", "-k", "-kT", "-kTn:1,2", $"--output={output}", .. sources]);
            await Assert.That(exitCode).IsEqualTo(0).Because(messages);

            var expected = Entries(output).Select(e => e.Key.Id).Concat(LogTemplates()).Distinct().Order(StringComparer.Ordinal).ToList();
            var actual = Entries(TemplateFile).Select(e => e.Key.Id).Order(StringComparer.Ordinal).ToList();
            await Assert.That(string.Join("\n", actual)).IsEqualTo(string.Join("\n", expected)).Because("po/seglcd.pot is outdated – run po/extract.sh");
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Cli_descriptions_reference_CliTexts()
    {
        var names = typeof(CliTexts).GetProperties(BindingFlags.Public | BindingFlags.Static).Select(p => p.Name).ToHashSet();
        var used = new HashSet<string>();
        foreach (var type in typeof(CliTexts).Assembly.GetTypes())
        {
            var descriptions = new List<(string Symbol, string? Description)>();
            if (type.GetCustomAttribute<CliCommandAttribute>() is { } command)
                descriptions.Add((type.Name, command.Description));
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (property.GetCustomAttribute<CliOptionAttribute>() is { } option)
                    descriptions.Add(($"{type.Name}.{property.Name}", option.Description));
                if (property.GetCustomAttribute<CliArgumentAttribute>() is { } argument)
                    descriptions.Add(($"{type.Name}.{property.Name}", argument.Description));
            }

            foreach (var (symbol, description) in descriptions)
            {
                await Assert.That(description is not null && names.Contains(description)).IsTrue()
                    .Because($"{symbol}: Description must be nameof(CliTexts.X), is \"{description}\"");
                used.Add(description!);
            }
        }

        foreach (var name in names)
            await Assert.That(used.Contains(name)).IsTrue().Because($"CliTexts.{name} is not used");
    }
}