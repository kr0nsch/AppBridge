using Netimobiledevice.Exceptions;
using Netimobiledevice.Usbmuxd;

namespace AppBridge.Core.Devices;

/// <summary>
/// Polls the Apple Mobile Device service (installed with the Apple Devices app or iTunes) for
/// iPhones connected over USB. Polling survives service restarts, unlike a single long-lived
/// subscription socket.
/// </summary>
public sealed class DeviceWatcher : IDeviceWatcher
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1.5);

    private readonly CancellationTokenSource _cts = new();
    private IReadOnlyList<ConnectedDevice> _devices = [];
    private bool? _serviceAvailable;
    private Task? _loop;

    /// <summary>Raised on a background thread whenever the device list or service state changes.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<ConnectedDevice> Devices => _devices;

    /// <summary>False when the Apple Mobile Device service cannot be reached; null before the first poll.</summary>
    public bool? ServiceAvailable => _serviceAvailable;

    public void Start() => _loop ??= Task.Run(() => RunAsync(_cts.Token));

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _cts.Dispose();
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Poll();
            try
            {
                await Task.Delay(Interval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void Poll()
    {
        bool available;
        List<ConnectedDevice> found = [];
        try
        {
            foreach (var device in Usbmux.GetDeviceList())
            {
                if (device.ConnectionType != UsbmuxdConnectionType.Usb || string.IsNullOrEmpty(device.Serial))
                    continue;
                if (found.All(d => d.Udid != device.Serial))
                    found.Add(new ConnectedDevice(device.Serial, device.DeviceId));
            }

            available = true;
        }
        catch (Exception ex) when (ex is UsbmuxConnectionException or UsbmuxException or IOException or System.Net.Sockets.SocketException)
        {
            available = false;
        }
        catch (Exception ex)
        {
            AppLog.Warn("Unexpected error while listing devices.", ex);
            available = _serviceAvailable ?? false;
        }

        var changed = available != _serviceAvailable || !found.Select(d => d.Udid).SequenceEqual(_devices.Select(d => d.Udid));
        if (!changed)
            return;

        if (available != _serviceAvailable)
            AppLog.Info(available ? "Apple Mobile Device service is available." : "Apple Mobile Device service is not reachable.");
        foreach (var added in found.Where(f => _devices.All(d => d.Udid != f.Udid)))
            AppLog.Info($"Device connected: {Redact(added.Udid)}");
        foreach (var removed in _devices.Where(d => found.All(f => f.Udid != d.Udid)))
            AppLog.Info($"Device disconnected: {Redact(removed.Udid)}");

        _serviceAvailable = available;
        _devices = found;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Keeps logs useful without writing full hardware identifiers.</summary>
    public static string Redact(string udid) => udid.Length <= 8 ? udid : $"{udid[..4]}…{udid[^4..]}";
}
