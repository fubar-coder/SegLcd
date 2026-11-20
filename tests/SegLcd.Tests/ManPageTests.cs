using System.Reflection;
using System.Text.RegularExpressions;

using DotMake.CommandLine;

using SegLcd.Daemon;
using SegLcd.Tool;

// Reflection over the CLI model; tests run on the JIT, never trimmed or AOT-compiled.
#pragma warning disable IL2026, IL2070, IL2075

namespace SegLcd.Tests;

/// <summary>Keeps the man pages (English and German) in sync with the commands, options, config keys and D-Bus API.</summary>
public sealed partial class ManPageTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "SegLcd.slnx")))
                return d.FullName;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    /// <summary>Man page text with roff escapes for '-' removed, so "\-\-cpu\-temp" matches "--cpu-temp".</summary>
    private static string Page(string relative) => File.ReadAllText(Path.Combine(Root, "man", relative)).Replace("\\-", "-", StringComparison.Ordinal);

    private static string Kebab(string name) => KebabRegex().Replace(name, "$1-$2").ToLowerInvariant();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex KebabRegex();

    private static IEnumerable<(string Command, string Option)> CliOptions() =>
        typeof(CliTexts).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<CliCommandAttribute>() is not null && t.Name.EndsWith("Command", StringComparison.Ordinal) && t.Name != "RootCliCommand")
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<CliOptionAttribute>() is not null)
                .Select(p => (Kebab(t.Name[..^"Command".Length]), "--" + Kebab(p.Name))));

    [Test]
    [Arguments("man1/seglcd.1")]
    [Arguments("de/man1/seglcd.1")]
    public async Task Ttseg_page_documents_every_command_and_option(string page)
    {
        var text = Page(page);
        var options = CliOptions().ToList();
        await Assert.That(options.Count).IsGreaterThan(40); // guards against the reflection silently finding nothing
        foreach (var (command, option) in options)
        {
            await Assert.That(text.Contains(".B " + command, StringComparison.Ordinal)).IsTrue().Because($"{page}: command '{command}' missing");
            await Assert.That(text.Contains(option, StringComparison.Ordinal)).IsTrue().Because($"{page}: option '{option}' ({command}) missing");
        }
    }

    [Test]
    [Arguments("man5/seglcdd.toml.5")]
    [Arguments("de/man5/seglcdd.toml.5")]
    public async Task Config_page_documents_every_key(string page)
    {
        var text = Page(page);
        var keys = typeof(DaemonConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.Name != nameof(DaemonConfig.Sensors))
            .Concat(typeof(SensorSection).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Select(p => Kebab(p.Name).Replace('-', '_'));
        foreach (var key in keys)
            await Assert.That(text.Contains(key, StringComparison.Ordinal)).IsTrue().Because($"{page}: key '{key}' missing");
    }

    [Test]
    [Arguments("man1/seglcdd.1")]
    [Arguments("de/man1/seglcdd.1")]
    public async Task Daemon_page_documents_the_dbus_interface(string page)
    {
        var text = Page(page);
        var xml = File.ReadAllText(Path.Combine(Root, "src", "SegLcd.Daemon", "DBus", "io.github.fubar_coder.SegLcd1.xml"));
        var members = MemberRegex().Matches(xml).Select(m => m.Groups[1].Value).ToList();
        await Assert.That(members.Count).IsGreaterThan(5);
        foreach (var member in members)
            await Assert.That(text.Contains(member, StringComparison.Ordinal)).IsTrue().Because($"{page}: D-Bus member '{member}' missing");
    }

    [GeneratedRegex("<(?:method|property) name=\"([A-Za-z]+)\"")]
    private static partial Regex MemberRegex();
}