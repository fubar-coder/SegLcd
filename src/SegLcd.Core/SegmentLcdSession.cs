namespace SegLcd;

/// <summary>
/// Keeps the display alive: the LCD only shows values while it receives update packets,
/// so this sends the current frame periodically (default every 250 ms, like the official software)
/// and reconnects after hot-unplug.
/// </summary>
public sealed class SegmentLcdSession : IAsyncDisposable
{
    private readonly Func<SegmentLcdDevice?> _connect;
    private TimeSpan _interval;
    private PeriodicTimer? _timer;
    private long _nextConnectAttempt;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private volatile SegmentLcdFrame _frame = new();
    private volatile byte[]? _rawPacket;
    private volatile SegmentLcdDevice? _device;

    /// <param name="connect">Factory used to (re)open the device; defaults to <see cref="SegmentLcdDevice.TryOpenFirst"/>.</param>
    /// <param name="interval">Send interval; defaults to <see cref="SegmentLcdProtocol.DefaultUpdateInterval"/>.</param>
    /// <param name="sendOffOnDispose">Blank the display when the session ends.</param>
    /// <remarks>Call <see cref="Start"/> to begin sending.</remarks>
    public SegmentLcdSession(Func<SegmentLcdDevice?>? connect = null, TimeSpan? interval = null, bool sendOffOnDispose = true)
    {
        _connect = connect ?? (() => SegmentLcdDevice.TryOpenFirst());
        _interval = interval ?? SegmentLcdProtocol.DefaultUpdateInterval;
        SendOffOnDispose = sendOffOnDispose;
    }

    /// <summary>
    /// Starts the background send loop. Set the first frame with <see cref="Update"/> or
    /// <see cref="UpdateRaw"/> before, otherwise an all-zero frame is sent first.
    /// </summary>
    public void Start() => _loop ??= Task.Run(() => RunAsync(_cts.Token));

    public bool SendOffOnDispose { get; set; }

    /// <summary>Send interval; can be changed while running (takes effect from the next tick).</summary>
    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _interval = value;
            if (_timer is { } t)
                t.Period = value;
        }
    }

    /// <summary>Minimum time between two reconnect attempts while no device is present.</summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>True while a device is open.</summary>
    public bool IsConnected => _device is not null;

    /// <summary>Path of the connected device, or <c>null</c>.</summary>
    public string? DevicePath => _device?.DevicePath;

    /// <summary>Model of the connected device, or <c>null</c>.</summary>
    public KnownDevice? Model => _device?.Model;

    /// <summary>The packet sent most recently (64 bytes), or <c>null</c>.</summary>
    public byte[]? LastPacket { get; private set; }

    /// <summary>Raised after every successful send with the packet that was written.</summary>
    public event Action<byte[]>? PacketSent;

    /// <summary>Raised when the connection state changes (device path or <c>null</c>).</summary>
    public event Action<string?>? ConnectionChanged;

    /// <summary>Replaces the frame that is sent from now on.</summary>
    public void Update(SegmentLcdFrame frame)
    {
        _rawPacket = null;
        _frame = frame;
    }

    /// <summary>Sends a raw 64-byte packet periodically instead of an encoded frame (for experiments).</summary>
    public void UpdateRaw(ReadOnlySpan<byte> packet)
    {
        var copy = new byte[SegmentLcdProtocol.PacketLength];
        packet[..Math.Min(packet.Length, copy.Length)].CopyTo(copy);
        _rawPacket = copy;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);
        _timer = timer;
        do
        {
            if (_device is null && Environment.TickCount64 >= _nextConnectAttempt)
            {
                _nextConnectAttempt = Environment.TickCount64 + (long)ReconnectDelay.TotalMilliseconds;
                _device = _connect();
                if (_device is not null)
                    ConnectionChanged?.Invoke(_device.DevicePath);
            }

            if (_device is null)
                continue;

            var packet = _rawPacket ?? SegmentLcdProtocol.EncodeUpdate(_frame);
            try
            {
                _device.SendPacket(packet);
                LastPacket = packet;
                PacketSent?.Invoke(packet);
            }
            catch (Exception e) when (e is IOException or TimeoutException or ObjectDisposedException)
            {
                _device.Dispose();
                _device = null;
                ConnectionChanged?.Invoke(null);
            }
        }
        while (await WaitAsync(timer, ct));

        _timer = null;
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        if (_loop is not null)
            await _loop;
        if (_device is not null)
        {
            if (SendOffOnDispose)
            {
                try
                {
                    _device.SendOff();
                }
                catch (Exception e) when (e is IOException or TimeoutException)
                {
                    // device already gone
                }
            }

            _device.Dispose();
            _device = null;
        }

        _cts.Dispose();
    }
}