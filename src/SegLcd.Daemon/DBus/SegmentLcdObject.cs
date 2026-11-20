using SegLcd.Linux;

using Tmds.DBus.Protocol;

namespace SegLcd.Daemon.DBus;

/// <summary>
/// Server-side implementation of <c>io.github.fubar_coder.SegLcd1</c>
/// (handler code generated from <c>DBus/io.github.fubar_coder.SegLcd1.xml</c>).
/// </summary>
internal sealed class SegmentLcdObject : DBusHandler, ISegLcd1Handler, ISegLcd1Properties
{
    public const string BusName = "io.github.fubar_coder.SegLcd1";
    public const string ObjectPath = "/io/github/fubar_coder/SegLcd1";

    private readonly DisplayController _controller;

    public SegmentLcdObject(DBusConnection connection, DisplayController controller)
        : base(connection, ObjectPath, handlesChildPaths: false, handleOnCapturedContext: false)
    {
        _controller = controller;
        _controller.Changed += OnChanged;
    }

    // --- properties -------------------------------------------------------------------------

    public bool Enabled
    {
        get => _controller.Enabled;
        set => _controller.SetEnabledAsync(value).GetAwaiter().GetResult();
    }

    public uint UpdateIntervalMs
    {
        get => (uint)_controller.UpdateIntervalMs;
        set => _controller.SetUpdateInterval(checked((int)value));
    }

    public uint SampleIntervalMs
    {
        get => (uint)_controller.SampleIntervalMs;
        set => _controller.SetSampleInterval(checked((int)value));
    }

    public bool Connected => _controller.Connected;

    public string DevicePath => _controller.DevicePath;

    public string Model => _controller.Model;

    public string ConfigPath => _controller.ConfigPath;

    // Fields without a source are reported as "" (the CLI shows "–").
    public Dictionary<string, string> Sources =>
        _controller.Sources.ToDictionary(kv => kv.Key, kv => kv.Value == "–" ? string.Empty : kv.Value);

    public Dictionary<string, string> SensorSpecs => _controller.SensorSpecs;

    public Dictionary<string, double> CurrentValues => _controller.CurrentValues;

    public byte[] LastPacket => _controller.LastPacket;

    public ValueTask HandleGetPropertyAsync(ISegLcd1Handler.GetPropertyContext context) => context.Handle(this);

    public ValueTask HandleGetAllPropertiesAsync(ISegLcd1Handler.GetAllPropertiesContext context) => context.Handle(this);

    public ValueTask HandleSetPropertyAsync(ISegLcd1Handler.SetPropertyContext context) => context.Handle(this);

    // --- methods ----------------------------------------------------------------------------

    public async ValueTask ReloadConfigAsync() => await _controller.ReloadAsync();

    public ValueTask SaveConfigAsync()
    {
        _controller.Save();
        return default;
    }

    public ValueTask<string> SetSensorAsync(string field, string spec) => new(_controller.SetSensor(field, spec));

    public async ValueTask<(string, string, string, double)[]> ListSensorsAsync()
    {
        var sensors = await SensorCatalog.EnumerateAsync(TimeSpan.FromMilliseconds(300));
        return [.. sensors.Select(s => (s.Spec, SensorCatalog.Name(s.Kind), s.Description, s.Value ?? double.NaN))];
    }

    // --- signals ----------------------------------------------------------------------------

    private void OnChanged(ControllerChange change)
    {
        Span<SegLcd1Property> changed = stackalloc SegLcd1Property[9];
        var n = 0;
        if (change.HasFlag(ControllerChange.Enabled))
            changed[n++] = SegLcd1Property.Enabled;
        if (change.HasFlag(ControllerChange.UpdateInterval))
            changed[n++] = SegLcd1Property.UpdateIntervalMs;
        if (change.HasFlag(ControllerChange.SampleInterval))
            changed[n++] = SegLcd1Property.SampleIntervalMs;
        if (change.HasFlag(ControllerChange.Connection))
        {
            changed[n++] = SegLcd1Property.Connected;
            changed[n++] = SegLcd1Property.DevicePath;
            changed[n++] = SegLcd1Property.Model;
        }

        if (change.HasFlag(ControllerChange.Sources))
        {
            changed[n++] = SegLcd1Property.Sources;
            changed[n++] = SegLcd1Property.SensorSpecs;
        }
        if (n > 0)
            Connection.EmitPropertiesChanged(ObjectPath, this, changed[..n]);
    }

    public void Detach() => _controller.Changed -= OnChanged;
}