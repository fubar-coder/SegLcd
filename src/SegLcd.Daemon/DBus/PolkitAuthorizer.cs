using Microsoft.Extensions.Logging;

using SegLcd.Daemon.DBus.Polkit;

using Tmds.DBus.Protocol;

namespace SegLcd.Daemon.DBus;

/// <summary>
/// Asks polkit whether a D-Bus caller may control the system service (action <see cref="ControlAction"/>, see
/// <c>packaging/polkit/</c>): the active local user without a prompt, root and group <c>seglcd</c> through the rules
/// file, everybody else after admin authentication (polkit shows the prompt through the caller's agent).
/// </summary>
internal sealed partial class PolkitAuthorizer(DBusConnection connection, ILogger log)
{
    public const string ControlAction = "io.github.fubar_coder.SegLcd1.control";
    private const uint AllowUserInteraction = 1;

    private readonly Authority _authority = new(connection, "org.freedesktop.PolicyKit1", "/org/freedesktop/PolicyKit1/Authority");

    /// <summary><c>true</c> if the D-Bus connection <paramref name="sender"/> (unique name) is authorized.</summary>
    public async Task<bool> IsAuthorizedAsync(string sender)
    {
        try
        {
            var subject = ("system-bus-name", new Dictionary<string, VariantValue> { ["name"] = VariantValue.String(sender) });
            var (authorized, _, _) = await _authority
                .CheckAuthorizationAsync(subject, ControlAction, [], AllowUserInteraction, string.Empty)
                .ConfigureAwait(false);
            return authorized;
        }
        catch (Exception e) when (e is DBusErrorReplyException or DBusConnectionException)
        {
            LogPolkitFailed(e.Message);
            return false;
        }
    }

    [LoggerMessage(EventId = 204, Level = LogLevel.Warning, Message = "polkit check failed, access denied: {Reason}")]
    private partial void LogPolkitFailed(string reason);
}
