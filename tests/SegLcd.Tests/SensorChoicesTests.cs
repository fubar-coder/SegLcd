using SegLcd.Client;
using SegLcd.Linux;

namespace SegLcd.Tests;

public class SensorChoicesTests
{
    private static readonly ServiceSensor[] Sensors =
    [
        new("nct6798:fan2", "fan", "hwmon5/fan2", 1200),
        new("k10temp:Tctl", "temperature", "hwmon4/temp1", 54.5),
        new("nct6798:fan1", "fan", "hwmon5/fan1", null),
        new("rapl:package", "power", "/sys/class/powercap/intel-rapl:0", 61.2),
    ];

    private static DisplayField Field(string name) => DisplayFields.All.Single(f => f.Name == name);

    [Test]
    public async Task Offers_automatic_first_then_sensors_of_the_field_kind_sorted()
    {
        var choices = SensorChoices.For(Field("pump"), Sensors, string.Empty);

        await Assert.That(choices.Select(c => c.Spec)).IsEquivalentTo(["", "nct6798:fan1", "nct6798:fan2"]);
        await Assert.That(choices[0].Spec).IsEqualTo(string.Empty);
        await Assert.That(choices[1].Spec).IsEqualTo("nct6798:fan1");
    }

    [Test]
    public async Task Keeps_a_configured_spec_that_is_not_listed()
    {
        var choices = SensorChoices.For(Field("cpu_power"), Sensors, "zenpower:SVI2_P_Core+zenpower:SVI2_P_SoC");

        await Assert.That(choices[^1].Spec).IsEqualTo("zenpower:SVI2_P_Core+zenpower:SVI2_P_SoC");
        await Assert.That(SensorChoices.IndexOf(choices, "zenpower:SVI2_P_Core+zenpower:SVI2_P_SoC")).IsEqualTo(choices.Count - 1);
        await Assert.That(SensorChoices.IndexOf(choices, "gone")).IsEqualTo(0);
    }

    [Test]
    public async Task Every_configurable_field_matches_the_catalog_kind()
    {
        // The GUI's kind per field must be the one the service reports in ListSensors.
        foreach (var field in DisplayFields.Configurable)
            await Assert.That(field.SensorKind).IsEqualTo(SensorCatalog.Name(SensorCatalog.KindOf(field.Name)));
        await Assert.That(DisplayFields.Configurable.Select(f => f.Name)).IsEquivalentTo(LinuxFrameSource.Fields.Configurable);
    }

    [Test]
    public async Task Formats_missing_values_as_dash()
    {
        await Assert.That(Field("fan").Format(null)).IsEqualTo("–");
        await Assert.That(Field("fan").Format(double.NaN)).IsEqualTo("–");
    }
}
