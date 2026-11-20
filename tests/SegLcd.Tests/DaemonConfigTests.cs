using SegLcd.Daemon;
using SegLcd.Linux;

namespace SegLcd.Tests;

public class DaemonConfigTests
{
    private static string TempFile() => Path.Combine(Path.GetTempPath(), $"seglcd-test-{Guid.NewGuid():N}.toml");

    [Test]
    public async Task Missing_file_yields_defaults()
    {
        var config = new ConfigStore(TempFile()).Load();

        await Assert.That(config.Enabled).IsTrue();
        await Assert.That(config.UpdateIntervalMs).IsEqualTo(250);
        await Assert.That(config.SampleIntervalMs).IsEqualTo(1000);
        await Assert.That(config.Sensors.Fan).IsNull();
    }

    [Test]
    public async Task Parses_snake_case_toml()
    {
        var path = TempFile();
        await File.WriteAllTextAsync(path, """
            enabled = false
            update_interval_ms = 200
            sample_interval_ms = 500

            [sensors]
            fan = "it8686_31050607:fan1"
            cpu_power = "zenpower:SVI2_P_Core+zenpower:SVI2_P_SoC"
            cpu_clock_mode = "max"
            """);
        try
        {
            var config = new ConfigStore(path).Load();
            var settings = config.Sensors.ToSettings();

            await Assert.That(config.Enabled).IsFalse();
            await Assert.That(config.UpdateIntervalMs).IsEqualTo(200);
            await Assert.That(settings.Fan).IsEqualTo("it8686_31050607:fan1");
            await Assert.That(settings.CpuPower).IsEqualTo("zenpower:SVI2_P_Core+zenpower:SVI2_P_SoC");
            await Assert.That(settings.CpuClockMode).IsEqualTo(CpuClockMode.Max);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Save_and_load_roundtrip()
    {
        var path = TempFile();
        var store = new ConfigStore(path);
        try
        {
            store.Save(new DaemonConfig { UpdateIntervalMs = 300, Sensors = new SensorSection { Pump = "hwmon5/fan5", CpuClockMode = "min" } });
            var loaded = store.Load();

            await Assert.That(loaded.UpdateIntervalMs).IsEqualTo(300);
            await Assert.That(loaded.Sensors.Pump).IsEqualTo("hwmon5/fan5");
            await Assert.That(loaded.Sensors.ToSettings().CpuClockMode).IsEqualTo(CpuClockMode.Min);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [Arguments(30)]
    [Arguments(500)]
    public async Task Rejects_update_interval_out_of_range(int ms)
    {
        var path = TempFile();
        await File.WriteAllTextAsync(path, $"update_interval_ms = {ms}\n");
        try
        {
            await Assert.That(() => new ConfigStore(path).Load()).Throws<InvalidDataException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Rejects_unknown_clock_mode()
    {
        var section = new SensorSection { CpuClockMode = "median" };

        await Assert.That(() => section.ToSettings()).Throws<ArgumentException>();
    }

    [Test]
    public async Task WithSpec_sets_field_and_empty_means_automatic()
    {
        var s = LinuxFrameSource.WithSpec(new SensorSettings(), LinuxFrameSource.Fields.Fan, " hwmon5/fan1 ");
        var cleared = LinuxFrameSource.WithSpec(s, LinuxFrameSource.Fields.Fan, "");

        await Assert.That(s.Fan).IsEqualTo("hwmon5/fan1");
        await Assert.That(cleared.Fan).IsNull();
        await Assert.That(() => LinuxFrameSource.WithSpec(s, "foo", "x")).Throws<ArgumentException>();
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SegLcd.slnx")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found"), relative);
    }

    private static string SnakeCase(string name) =>
        string.Concat(name.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    [Test]
    public async Task Example_config_contains_every_option()
    {
        var lines = (await File.ReadAllLinesAsync(RepoFile("packaging/config.example.toml")))
            .Select(l => l.Trim())
            .Where(l => !l.StartsWith('#'))
            .ToList();
        var top = typeof(DaemonConfig).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Where(p => p.Name != nameof(DaemonConfig.Sensors)).Select(p => SnakeCase(p.Name));
        var sensors = typeof(SensorSection).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Select(p => SnakeCase(p.Name));

        foreach (var key in top.Concat(sensors))
            await Assert.That(lines.Any(l => l.StartsWith(key + " =", StringComparison.Ordinal))).IsTrue().Because($"'{key}' fehlt in config.example.toml");
    }

    [Test]
    public async Task Example_config_equals_defaults()
    {
        var example = new ConfigStore(RepoFile("packaging/config.example.toml")).Load();
        var defaults = new DaemonConfig();

        await Assert.That(example.Enabled).IsEqualTo(defaults.Enabled);
        await Assert.That(example.UpdateIntervalMs).IsEqualTo(defaults.UpdateIntervalMs);
        await Assert.That(example.SampleIntervalMs).IsEqualTo(defaults.SampleIntervalMs);
        await Assert.That(string.IsNullOrEmpty(example.Device)).IsTrue();
        await Assert.That(example.Sensors.ToSettings()).IsEqualTo(defaults.Sensors.ToSettings());
    }
}