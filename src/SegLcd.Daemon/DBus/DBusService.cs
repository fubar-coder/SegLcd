using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Tmds.DBus.Protocol;

namespace SegLcd.Daemon.DBus;

/// <summary>Publishes <see cref="SegmentLcdObject"/> on the session bus. The display keeps running if D-Bus is unavailable.</summary>
internal sealed partial class DBusService(DisplayController controller, DaemonOptions options, ILogger<DBusService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var address = options.Mode == DaemonMode.System ? DBusAddress.System : DBusAddress.Session;
        if (address is null)
        {
            LogNoBus(options.Mode == DaemonMode.System ? "system" : "session");
            return;
        }

        using var connection = new DBusConnection(address);
        SegmentLcdObject? obj = null;
        try
        {
            await connection.ConnectAsync();
            var authorizer = options.Mode == DaemonMode.System ? new PolkitAuthorizer(connection, log) : null;
            obj = new SegmentLcdObject(connection, controller, authorizer);
            connection.AddMethodHandler(obj);
            if (!await connection.TryRequestNameAsync(SegmentLcdObject.BusName))
            {
                LogNameTaken(SegmentLcdObject.BusName);
                return;
            }

            LogReady(SegmentLcdObject.BusName, SegmentLcdObject.ObjectPath, options.Mode == DaemonMode.System ? "system" : "session");
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

    [LoggerMessage(EventId = 200, Level = LogLevel.Warning, Message = "No D-Bus {Bus} bus available – D-Bus interface disabled")]
    private partial void LogNoBus(string bus);

    [LoggerMessage(EventId = 201, Level = LogLevel.Error, Message = "D-Bus name {Name} is already taken – is seglcdd already running?")]
    private partial void LogNameTaken(string name);

    [LoggerMessage(EventId = 202, Level = LogLevel.Information, Message = "D-Bus ready: {Name} {Path} ({Bus} bus)")]
    private partial void LogReady(string name, string path, string bus);

    [LoggerMessage(EventId = 203, Level = LogLevel.Error, Message = "D-Bus error: {Reason}")]
    private partial void LogDBusFailed(string reason);
}