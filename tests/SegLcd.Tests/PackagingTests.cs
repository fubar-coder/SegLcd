namespace SegLcd.Tests;

/// <summary>
/// The AUR, RPM and DEB recipes spell out every installation step (no shared build script). These tests keep
/// them in sync: every packaged file must be installed by every recipe, to the same destination.
/// </summary>
public sealed class PackagingTests
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

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    /// <summary>Source files that every recipe must install.</summary>
    private static readonly string[] Sources =
    [
        "packaging/systemd/system/seglcdd.service",
        "packaging/sysusers/seglcd.conf",
        "packaging/udev/70-seglcd.rules",
        "packaging/dbus/system/io.github.fubar_coder.SegLcd1.conf",
        "packaging/dbus/system/io.github.fubar_coder.SegLcd1.service",
        "packaging/polkit/io.github.fubar_coder.SegLcd1.policy",
        "packaging/polkit/50-seglcd.rules",
        "packaging/config.example.toml",
        "man/man1/seglcd.1", "man/man1/seglcdd.1", "man/man5/seglcdd.toml.5",
        "man/de/man1/seglcd.1", "man/de/man1/seglcdd.1", "man/de/man5/seglcdd.toml.5",
        "README.md", "README.de.md",
        "udev/72-rapl-energy-read.rules", "udev/rapl.sysusers.conf",
        // seglcd-gtk
        "data/io.github.fubar_coder.SegLcd.desktop",
        "data/io.github.fubar_coder.SegLcd.metainfo.xml",
        "data/icons/hicolor/scalable/apps/io.github.fubar_coder.SegLcd.svg",
        "man/man1/seglcd-gtk.1", "man/de/man1/seglcd-gtk.1",
    ];

    /// <summary>Destinations (relative to the package root) that every recipe must create.</summary>
    private static readonly string[] Destinations =
    [
        "usr/bin/seglcd",
        "usr/bin/seglcdd",
        "usr/lib/systemd/system/seglcdd.service",
        "usr/lib/sysusers.d/seglcd.conf",
        "usr/lib/udev/rules.d/70-seglcd.rules",
        "usr/share/dbus-1/system.d/io.github.fubar_coder.SegLcd1.conf",
        "usr/share/dbus-1/system-services/io.github.fubar_coder.SegLcd1.service",
        "usr/share/polkit-1/actions/io.github.fubar_coder.SegLcd1.policy",
        "usr/share/polkit-1/rules.d/50-seglcd.rules",
        "etc/seglcd/config.toml",
        // seglcd-gtk
        "usr/bin/seglcd-gtk",
        "usr/share/applications/io.github.fubar_coder.SegLcd.desktop",
        "usr/share/metainfo/io.github.fubar_coder.SegLcd.metainfo.xml",
        "usr/share/icons/hicolor/scalable/apps/io.github.fubar_coder.SegLcd.svg",
        "usr/share/man/man1/seglcd-gtk.1",
        "usr/share/man/de/man1/seglcd-gtk.1",
    ];

    private static readonly Dictionary<string, string> RpmMacros = new()
    {
        ["%{buildroot}"] = "",
        ["%{_bindir}"] = "/usr/bin",
        ["%{_unitdir}"] = "/usr/lib/systemd/system",
        ["%{_sysusersdir}"] = "/usr/lib/sysusers.d",
        ["%{_udevrulesdir}"] = "/usr/lib/udev/rules.d",
        ["%{_sysconfdir}"] = "/etc",
        ["%{_mandir}"] = "/usr/share/man",
        ["%{_docdir}"] = "/usr/share/doc",
        ["%{_datadir}"] = "/usr/share",
        ["%{name}"] = "seglcd",
    };

    /// <summary>Recipe text with the package-root variable removed (and RPM macros expanded).</summary>
    public static IEnumerable<(string Recipe, string Text)> Recipes()
    {
        yield return ("AUR seglcd", Read("packaging/arch/seglcd/PKGBUILD").Replace("\"$pkgdir/", "\"/", StringComparison.Ordinal));
        yield return ("AUR seglcd-git", Read("packaging/arch/seglcd-git/PKGBUILD").Replace("\"$pkgdir/", "\"/", StringComparison.Ordinal));

        var spec = Read("packaging/rpm/seglcd.spec");
        foreach (var (macro, value) in RpmMacros)
            spec = spec.Replace(macro, value, StringComparison.Ordinal);
        yield return ("RPM", spec);

        yield return ("DEB", Read("packaging/debian/rules").Replace("$(PKG)/", "/", StringComparison.Ordinal).Replace("$(GTK)/", "/", StringComparison.Ordinal)
                             + Read("packaging/debian/seglcd.docs") + Read("packaging/debian/seglcd.examples"));
    }

    [Test]
    public async Task Every_recipe_installs_every_source_file()
    {
        foreach (var (recipe, text) in Recipes())
        {
            foreach (var source in Sources)
                await Assert.That(text.Contains(source, StringComparison.Ordinal)).IsTrue().Because($"{recipe} does not install {source}");
        }
    }

    [Test]
    public async Task Every_recipe_creates_every_destination()
    {
        foreach (var (recipe, text) in Recipes())
        {
            foreach (var destination in Destinations)
                await Assert.That(text.Contains("/" + destination, StringComparison.Ordinal)).IsTrue().Because($"{recipe} does not create /{destination}");
        }
    }

    [Test]
    public async Task Rpm_files_section_lists_every_destination()
    {
        var spec = Recipes().Single(r => r.Recipe == "RPM").Text;
        var files = spec[spec.IndexOf("%files", StringComparison.Ordinal)..];
        foreach (var destination in Destinations)
            await Assert.That(files.Contains("/" + destination, StringComparison.Ordinal)).IsTrue().Because($"%files misses /{destination}");
    }

    [Test]
    public async Task Every_system_packaging_file_is_covered()
    {
        // A new file in one of these directories must be added to Sources (and thus to every recipe).
        string[] dirs = ["packaging/systemd/system", "packaging/sysusers", "packaging/udev", "packaging/dbus/system", "packaging/polkit", "data"];
        foreach (var dir in dirs)
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(Root, dir), "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(Root, file).Replace('\\', '/');
                await Assert.That(Sources.Contains(relative)).IsTrue().Because($"{relative} is not packaged");
            }
        }
    }

    [Test]
    public async Task Gui_files_go_into_the_gui_package()
    {
        // The GUI is a separate package: its files are installed in the seglcd-gtk part of each recipe only.
        string[] gui = ["seglcd-gtk", "io.github.fubar_coder.SegLcd.desktop"];
        var aur = Read("packaging/arch/seglcd/PKGBUILD");
        var aurGui = aur[aur.IndexOf("package_seglcd-gtk()", StringComparison.Ordinal)..];
        var aurMain = aur[..aur.IndexOf("package_seglcd-gtk()", StringComparison.Ordinal)];
        var rpm = Read("packaging/rpm/seglcd.spec");
        var rpmFiles = rpm[rpm.IndexOf("%files\n", StringComparison.Ordinal)..rpm.IndexOf("%files gtk", StringComparison.Ordinal)];
        foreach (var name in gui)
        {
            await Assert.That(aurGui).Contains(name);
            await Assert.That(rpmFiles).DoesNotContain(name);
        }

        await Assert.That(aurMain).DoesNotContain("/usr/bin/seglcd-gtk");
        await Assert.That(aurMain).DoesNotContain("share/applications");

        await Assert.That(Read("packaging/debian/rules")).Contains("$(GTK)/usr/bin/seglcd-gtk");
    }

    [Test]
    public async Task Recipes_use_the_release_version()
    {
        var props = Read("Directory.Build.props");
        var version = props[(props.IndexOf("<Version>", StringComparison.Ordinal) + "<Version>".Length)..props.IndexOf("</Version>", StringComparison.Ordinal)];

        await Assert.That(Read("packaging/arch/seglcd/PKGBUILD")).Contains($"pkgver={version}\n");
        await Assert.That(Read("packaging/rpm/seglcd.spec")).Contains($"Version:        {version}\n");
        await Assert.That(Read("packaging/debian/changelog")).StartsWith($"seglcd ({version}-");
    }
}