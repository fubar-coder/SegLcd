using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Tmds.DBus.Protocol;

namespace SegLcd.Daemon.DBus;

/// <summary>Publishes <see cref="SegmentLcdObject"/> on the session bus. The display keeps running if D-Bus is unavailable.</summary>
internal sealed partial class DBusService(DisplayController controller, ILogger<DBusService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var address = DBusAddress.Session;
        if (address is null)
        {
            LogNoSessionBus();
            return;
        }

        using var connection = new DBusConnection(address);
        SegmentLcdObject? obj = null;
        try
        {
            await connection.ConnectAsync();
            obj = new SegmentLcdObject(connection, controller);
            connection.AddMethodHandler(obj);
            if (!await connection.TryRequestNameAsync(SegmentLcdObject.BusName))
            {
                LogNameTaken(SegmentLcdObject.BusName);
                return;
            }

            LogReady(SegmentLcdObject.BusName, SegmentLcdObject.ObjectPath);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception e) when (e is DBusConnectionException or DBusErrorReplyException or IOException)
        {
            LogDBusFailed(e.Message);
        }
        finally
        {
            obj?.Detach();
        }
    }

    [LoggerMessage(EventId = 200, Level = LogLevel.Warning, Message = "No session bus (DBUS_SESSION_BUS_ADDRESS not set) – D-Bus interface disabled")]
    private partial void LogNoSessionBus();

    [LoggerMessage(EventId = 201, Level = LogLevel.Error, Message = "D-Bus name {Name} is already taken – is seglcdd already running?")]
    private partial void LogNameTaken(string name);

    [LoggerMessage(EventId = 202, Level = LogLevel.Information, Message = "D-Bus ready: {Name} {Path}")]
    private partial void LogReady(string name, string path);

    [LoggerMessage(EventId = 203, Level = LogLevel.Error, Message = "D-Bus error: {Reason}")]
    private partial void LogDBusFailed(string reason);
}