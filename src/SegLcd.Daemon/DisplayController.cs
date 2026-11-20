using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SegLcd.Linux;
using SegLcd.Localization;

namespace SegLcd.Daemon;

/// <summary>Which observable state changed (for D-Bus PropertiesChanged).</summary>
[Flags]
public enum ControllerChange
{
    None = 0,
    Enabled = 1,
    UpdateInterval = 2,
    SampleInterval = 4,
    Connection = 8,
    Sources = 16,
    CpuClockMode = 32,
}

/// <summary>
/// Reads the sensors, keeps the display session alive and applies runtime changes
/// (from D-Bus or a config reload). All public members are thread-safe.
/// </summary>
public sealed partial class DisplayController(ConfigStore store, ILogger<DisplayController> log) : BackgroundService
{
    private readonly Lock _lock = new();
    private DaemonConfig _config = new();
    private LinuxFrameSource _source = new(new SensorSettings());
    private SegmentLcdSession? _session;
    private SegmentLcdFrame? _lastFrame;
    private CancellationTokenSource _sampleDelay = new();

    /// <summary>Raised after a change; handlers must not block.</summary>
    public event Action<ControllerChange>? Changed;

    public string ConfigPath => store.Path;

    public bool Enabled
    {
        get
        {
            lock (_lock)
                return _config.Enabled;
        }
    }

    public int UpdateIntervalMs
    {
        get
        {
            lock (_lock)
                return _config.UpdateIntervalMs;
        }
    }

    public int SampleIntervalMs
    {
        get
        {
            lock (_lock)
                return _config.SampleIntervalMs;
        }
    }

    public bool Connected => _session?.IsConnected ?? false;

    public string DevicePath => _session?.DevicePath ?? string.Empty;

    public string Model => _session?.Model?.Name ?? string.Empty;

    public byte[] LastPacket => _session?.LastPacket ?? [];

    public IReadOnlyDictionary<string, string> Sources
    {
        get
        {
            lock (_lock)
                return _source.Sources;
        }
    }

    /// <summary>Configured spec per field (empty = automatic).</summary>
    public Dictionary<string, string> SensorSpecs
    {
        get
        {
            lock (_lock)
                return LinuxFrameSource.Fields.Configurable.ToDictionary(f => f, f => LinuxFrameSource.GetSpec(_source.Settings, f) ?? string.Empty);
        }
    }

    /// <summary>Values of the frame sent last (only fields with a value).</summary>
    public Dictionary<string, double> CurrentValues
    {
        get
        {
            var f = _lastFrame;
            var result = new Dictionary<string, double>();
            if (f is null)
                return result;
            void Add(string key, double? v)
            {
                if (v is { } x && double.IsFinite(x))
                    result[key] = x;
            }

            Add("cpu_temperature", f.CpuTemperature);
            Add("cpu_usage", f.CpuUsage);
            Add("cpu_power", f.CpuPower);
            Add("cpu_clock", f.CpuClock);
            Add("cpu_voltage", f.CpuVoltage);
            Add("gpu_temperature", f.GpuTemperature);
            Add("gpu_usage", f.GpuUsage);
            Add("gpu_power", f.GpuPower);
            Add("gpu_clock", f.GpuClock);
            Add("fan", f.FanSpeed);
            Add("pump", f.PumpSpeed);
            Add("ram_usage", f.RamUsage);
            return result;
        }
    }

    public async Task SetEnabledAsync(bool enabled)
    {
        lock (_lock)
        {
            if (_config.Enabled == enabled)
                return;
            _config.Enabled = enabled;
        }

        await ApplySessionAsync();
        if (enabled)
            LogDisplayEnabled();
        else
            LogDisplayDisabled();
        Changed?.Invoke(ControllerChange.Enabled | ControllerChange.Connection);
    }

    public void SetUpdateInterval(int ms)
    {
        if (ms < DaemonConfig.MinUpdateIntervalMs || ms > DaemonConfig.MaxUpdateIntervalMs)
            throw new ArgumentException(Texts.Current.T("UpdateIntervalMs must be between {0} and {1}.", DaemonConfig.MinUpdateIntervalMs, DaemonConfig.MaxUpdateIntervalMs));
        lock (_lock)
        {
            _config.UpdateIntervalMs = ms;
            if (_session is not null)
                _session.Interval = TimeSpan.FromMilliseconds(ms);
        }

        Changed?.Invoke(ControllerChange.UpdateInterval);
    }

    public void SetSampleInterval(int ms)
    {
        if (ms < DaemonConfig.MinSampleIntervalMs || ms > DaemonConfig.MaxSampleIntervalMs)
            throw new ArgumentException(Texts.Current.T("SampleIntervalMs must be between {0} and {1}.", DaemonConfig.MinSampleIntervalMs, DaemonConfig.MaxSampleIntervalMs));
        lock (_lock)
            _config.SampleIntervalMs = ms;
        WakeSampler();
        Changed?.Invoke(ControllerChange.SampleInterval);
    }

    /// <summary>How the CPU clock is derived from the cores: <c>avg</c>, <c>max</c>, <c>min</c> or <c>first</c>.</summary>
    public string CpuClockMode
    {
        get
        {
            lock (_lock)
                return _source.Settings.CpuClockMode.ToString().ToLowerInvariant();
        }
    }

    /// <exception cref="ArgumentException">Unknown mode.</exception>
    public void SetCpuClockMode(string mode)
    {
        if (!Enum.TryParse<CpuClockMode>(mode, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed) || int.TryParse(mode, out _))
            throw new ArgumentException(Texts.Current.T("sensors.cpu_clock_mode '{0}' is invalid (avg, max, min, first).", mode));
        lock (_lock)
            _source = new LinuxFrameSource(_source.Settings with { CpuClockMode = parsed });
        WakeSampler();
        Changed?.Invoke(ControllerChange.CpuClockMode);
    }

    /// <summary>Changes the source of one field; returns the resolved description.</summary>
    /// <exception cref="ArgumentException">Unknown field or sensor not found.</exception>
    public string SetSensor(string field, string spec)
    {
        var settings = LinuxFrameSource.WithSpec(_source.Settings, field, spec); // validates the field name
        if (!string.IsNullOrWhiteSpace(spec))
            LinuxFrameSource.Validate(spec.Trim());
        var source = new LinuxFrameSource(settings);
        lock (_lock)
        {
            _source = source;
            _config.Sensors = SensorSection.From(settings);
        }

        WakeSampler();
        Changed?.Invoke(ControllerChange.Sources);
        return source.Sources[field];
    }

    /// <summary>Re-reads the config file and applies it.</summary>
    public async Task ReloadAsync()
    {
        var config = store.Load();
        var source = new LinuxFrameSource(config.Sensors.ToSettings());
        bool deviceChanged;
        lock (_lock)
        {
            deviceChanged = !string.Equals(_config.Device, config.Device, StringComparison.Ordinal);
            _config = config;
            _source = source;
            if (_session is not null)
                _session.Interval = TimeSpan.FromMilliseconds(config.UpdateIntervalMs);
        }

        if (deviceChanged)
            await StopSessionAsync();
        await ApplySessionAsync();
        WakeSampler();
        if (File.Exists(store.Path))
            LogConfigLoaded(store.Path);
        else
            LogConfigMissing(store.Path);
        if (store.StatePath is { } statePath && File.Exists(statePath))
            LogSavedSettingsApplied(statePath);
        Changed?.Invoke(ControllerChange.Enabled | ControllerChange.UpdateInterval | ControllerChange.SampleInterval
                        | ControllerChange.Sources | ControllerChange.CpuClockMode | ControllerChange.Connection);
    }

    /// <summary>Writes the current runtime settings to the config file.</summary>
    public void Save()
    {
        DaemonConfig copy;
        lock (_lock)
        {
            copy = new DaemonConfig
            {
                Enabled = _config.Enabled,
                UpdateIntervalMs = _config.UpdateIntervalMs,
                SampleIntervalMs = _config.SampleIntervalMs,
                Device = _config.Device,
                Sensors = SensorSection.From(_source.Settings),
            };
        }

        store.Save(copy);
        LogConfigSaved(store.SavePath);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ReloadAsync();
        }
        catch (InvalidDataException e)
        {
            LogConfigError(e.Message);
            await ApplySessionAsync(); // run with defaults
        }

        foreach (var (field, source) in Sources)
            LogSource(field, source);

        while (!stoppingToken.IsCancellationRequested)
        {
            LinuxFrameSource source;
            int delay;
            CancellationTokenSource wake;
            lock (_lock)
            {
                source = _source;
                delay = _config.SampleIntervalMs;
                wake = _sampleDelay;
            }

            var frame = source.Sample();
            _lastFrame = frame;
            _session?.Update(frame);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, wake.Token);
            try
            {
                await Task.Delay(delay, linked.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // woken up because settings changed
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await StopSessionAsync(); // sends the off packet
    }

    private async Task ApplySessionAsync()
    {
        bool enabled;
        lock (_lock)
            enabled = _config.Enabled;

        if (!enabled)
        {
            await StopSessionAsync();
            return;
        }

        lock (_lock)
        {
            if (_session is not null)
                return;
            var device = _config.Device;
            var session = new SegmentLcdSession(
                () => SegmentLcdDevice.TryOpenFirst(string.IsNullOrWhiteSpace(device) ? null : device),
                TimeSpan.FromMilliseconds(_config.UpdateIntervalMs),
                sendOffOnDispose: true);
            session.ConnectionChanged += OnConnectionChanged;
            if (_lastFrame is { } frame)
                session.Update(frame);
            session.Start();
            _session = session;
        }
    }

    private async Task StopSessionAsync()
    {
        SegmentLcdSession? session;
        lock (_lock)
        {
            session = _session;
            _session = null;
        }

        if (session is null)
            return;
        session.ConnectionChanged -= OnConnectionChanged;
        await session.DisposeAsync();
        Changed?.Invoke(ControllerChange.Connection);
    }

    private void OnConnectionChanged(string? path)
    {
        if (path is null)
            LogDisconnected();
        else
            LogConnected(path, Model);
        Changed?.Invoke(ControllerChange.Connection);
    }

    private void WakeSampler()
    {
        CancellationTokenSource old;
        lock (_lock)
        {
            old = _sampleDelay;
            _sampleDelay = new CancellationTokenSource();
        }

        old.Cancel();
        old.Dispose();
    }

    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "Display connected: {Path} ({Model})")]
    private partial void LogConnected(string path, string model);

    [LoggerMessage(EventId = 101, Level = LogLevel.Warning, Message = "Display disconnected – waiting for the device")]
    private partial void LogDisconnected();

    [LoggerMessage(EventId = 102, Level = LogLevel.Information, Message = "Display enabled")]
    private partial void LogDisplayEnabled();

    [LoggerMessage(EventId = 103, Level = LogLevel.Information, Message = "Display disabled, device released")]
    private partial void LogDisplayDisabled();

    [LoggerMessage(EventId = 110, Level = LogLevel.Information, Message = "Configuration loaded: {Path}")]
    private partial void LogConfigLoaded(string path);

    [LoggerMessage(EventId = 114, Level = LogLevel.Information, Message = "No configuration file at {Path}, using defaults")]
    private partial void LogConfigMissing(string path);

    [LoggerMessage(EventId = 115, Level = LogLevel.Information, Message = "Saved settings applied: {Path}")]
    private partial void LogSavedSettingsApplied(string path);

    [LoggerMessage(EventId = 111, Level = LogLevel.Information, Message = "Configuration saved: {Path}")]
    private partial void LogConfigSaved(string path);

    [LoggerMessage(EventId = 112, Level = LogLevel.Error, Message = "Invalid configuration, using defaults: {Reason}")]
    private partial void LogConfigError(string reason);

    [LoggerMessage(EventId = 113, Level = LogLevel.Information, Message = "Source {Field}: {Source}")]
    private partial void LogSource(string field, string source);
}