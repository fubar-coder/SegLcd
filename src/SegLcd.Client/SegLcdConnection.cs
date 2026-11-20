using SegLcd.Client.DBus;

using Tmds.DBus.Protocol;

namespace SegLcd.Client;

/// <summary>Bus the service was found on.</summary>
public enum ServiceBus
{
    /// <summary>System service (<c>seglcdd --system</c>, packages).</summary>
    System,

    /// <summary>User service (<c>systemctl --user</c>).</summary>
    Session,
}

/// <summary>State of the service (all properties except the values, which change without a signal).</summary>
public sealed record ServiceState(
    bool Enabled,
    uint UpdateIntervalMs,
    uint SampleIntervalMs,
    bool Connected,
    string DevicePath,
    string Model,
    string ConfigPath,
    IReadOnlyDictionary<string, string> SensorSpecs,
    IReadOnlyDictionary<string, string> Sources);

/// <summary>A sensor offered by the service (see D-Bus <c>ListSensors</c>); <c>Value</c> is <c>null</c> if not readable.</summary>
public sealed record ServiceSensor(string Spec, string Kind, string Description, double? Value);

/// <summary>
/// Connection to <c>seglcdd</c> over D-Bus. <see cref="ConnectAsync"/> prefers the system service and falls back to the
/// user service; <see cref="Lost"/> is cancelled when the service goes away.
/// </summary>
public sealed class SegLcdConnection : IDisposable
{
    public const string BusName = "io.github.fubar_coder.SegLcd1";
    public const string ObjectPath = "/io/github/fubar_coder/SegLcd1";
    private const string AccessDenied = "org.freedesktop.DBus.Error.AccessDenied";

    private readonly DBusConnection _connection;
    private readonly NameOwnerWatcher _watcher;
    private readonly SegLcd1 _proxy;

    private SegLcdConnection(DBusConnection connection, NameOwnerWatcher watcher, ServiceBus bus, CancellationToken lost)
    {
        _connection = connection;
        _watcher = watcher;
        _proxy = new SegLcd1(connection, BusName, ObjectPath);
        Bus = bus;
        Lost = lost;
    }

    public ServiceBus Bus { get; }

    /// <summary>Cancelled when the service stops or the bus connection ends.</summary>
    public CancellationToken Lost { get; }

    /// <summary>
    /// Finds the service: first a running one (system bus, then session bus), then an activatable one, which is started
    /// by the first call. <paramref name="only"/> restricts the search to one bus. Returns <c>null</c> if not found.
    /// </summary>
    public static async Task<SegLcdConnection?> ConnectAsync(ServiceBus? only = null, CancellationToken cancellationToken = default)
    {
        var buses = new List<(ServiceBus Bus, DBusConnection Connection, bool Running, bool Activatable)>();
        try
        {
            foreach (var (bus, address) in new[] { (ServiceBus.System, DBusAddress.System), (ServiceBus.Session, DBusAddress.Session) })
            {
                if (string.IsNullOrEmpty(address) || (only is { } o && o != bus))
                    continue;
                var connection = new DBusConnection(address);
                try
                {
                    await connection.ConnectAsync().ConfigureAwait(false);
                    var running = (await connection.ListServicesAsync().ConfigureAwait(false)).Contains(BusName);
                    var activatable = (await connection.ListActivatableServicesAsync().ConfigureAwait(false)).Contains(BusName);
                    buses.Add((bus, connection, running, activatable));
                }
                catch (Exception e) when (e is DBusConnectionException or IOException)
                {
                    connection.Dispose();
                }
            }

            var chosen = buses.FirstOrDefault(b => b.Running);
            if (chosen.Connection is null)
                chosen = buses.FirstOrDefault(b => b.Activatable);
            if (chosen.Connection is null)
                return null;

            // A call activates the service if needed; then watch its owner.
            await new SegLcd1(chosen.Connection, BusName, ObjectPath).GetConnectedAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            var watcher = await chosen.Connection.WatchNameOwnerAsync(BusName).ConfigureAwait(false);
            var owner = await watcher.WaitForOwnerAsync(cancellationToken).ConfigureAwait(false);
            var result = new SegLcdConnection(chosen.Connection, watcher, chosen.Bus, watcher.GetOwnerChangedCancellationToken(owner));
            buses.Remove(chosen);
            return result;
        }
        finally
        {
            foreach (var b in buses)
                b.Connection.Dispose();
        }
    }

    /// <summary><c>true</c> if <paramref name="e"/> is the D-Bus policy refusing a control call (not in group <c>seglcd</c>).</summary>
    public static bool IsAccessDenied(Exception e) => e is DBusErrorReplyException { ErrorName: AccessDenied };

    /// <summary>Error text sent by the service (D-Bus error message), otherwise the exception message.</summary>
    public static string ErrorText(Exception e) => e is DBusErrorReplyException reply ? reply.ErrorMessage : e.Message;

    public async Task<ServiceState> GetStateAsync()
    {
        var p = await _proxy.GetPropertiesAsync().ConfigureAwait(false);
        return new ServiceState(p.Enabled, p.UpdateIntervalMs, p.SampleIntervalMs, p.Connected, p.DevicePath, p.Model,
            p.ConfigPath, p.SensorSpecs, p.Sources);
    }

    public Task<Dictionary<string, double>> GetCurrentValuesAsync() => _proxy.GetCurrentValuesAsync();

    public async Task<IReadOnlyList<ServiceSensor>> ListSensorsAsync() =>
        [.. (await _proxy.ListSensorsAsync().ConfigureAwait(false))
            .Select(s => new ServiceSensor(s.Item1, s.Item2, s.Item3, double.IsNaN(s.Item4) ? null : s.Item4))];

    public Task SetEnabledAsync(bool value) => _proxy.SetEnabledAsync(value);

    public Task SetUpdateIntervalMsAsync(uint value) => _proxy.SetUpdateIntervalMsAsync(value);

    public Task SetSampleIntervalMsAsync(uint value) => _proxy.SetSampleIntervalMsAsync(value);

    /// <summary>Sets the sensor of <paramref name="field"/> (empty = automatic); returns the resolved source.</summary>
    public Task<string> SetSensorAsync(string field, string spec) => _proxy.SetSensorAsync(field, spec);

    public Task SaveConfigAsync() => _proxy.SaveConfigAsync();

    public Task ReloadConfigAsync() => _proxy.ReloadConfigAsync();

    /// <summary>
    /// Calls <paramref name="changed"/> after a property changed. The callback runs on the synchronization context of
    /// the caller (the GTK thread in the GUI).
    /// </summary>
    public ValueTask<IDisposable> WatchChangesAsync(Action changed) =>
        _proxy.WatchPropertiesChangedAsync(_ => changed(), emitOnCapturedContext: true);

    public void Dispose()
    {
        _watcher.Dispose();
        _connection.Dispose();
    }
}
