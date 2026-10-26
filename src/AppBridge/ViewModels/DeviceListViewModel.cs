using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AppBridge.Core;
using AppBridge.Core.Devices;
using AppBridge.Services;
using Microsoft.UI.Dispatching;

namespace AppBridge.ViewModels;

/// <summary>
/// The iPhones currently plugged in. New devices are inspected automatically and, if this PC
/// is not trusted yet, the phone is asked to show its "Trust This Computer?" prompt.
/// </summary>
public sealed partial class DeviceListViewModel : ObservableObject
{
    private readonly IDeviceWatcher _watcher;
    private readonly IDeviceService _devices;
    private readonly DispatcherQueue _dispatcher;

    public DeviceListViewModel(IDeviceWatcher watcher, IDeviceService devices, DispatcherQueue dispatcher)
    {
        _watcher = watcher;
        _devices = devices;
        _dispatcher = dispatcher;
        _watcher.Changed += (_, _) => _dispatcher.TryEnqueue(Sync);
        Sync();
    }

    public ObservableCollection<DeviceItemViewModel> Devices { get; } = [];

    /// <summary>False when the Apple Mobile Device service (Apple Devices app) is missing or stopped.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsServiceMissing))]
    public partial bool IsServiceAvailable { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(HasDevices))]
    public partial int Count { get; set; }

    public bool IsServiceMissing => !IsServiceAvailable;

    public bool IsEmpty => Count == 0;

    public bool HasDevices => Count > 0;

    /// <summary>Raised after the list changed or a device became ready.</summary>
    public event EventHandler? StateChanged;

    public DeviceItemViewModel? Find(string? udid) => Devices.FirstOrDefault(d => d.Udid == udid);

    private void Sync()
    {
        IsServiceAvailable = _watcher.ServiceAvailable ?? true;
        var current = _watcher.Devices;

        foreach (var gone in Devices.Where(d => current.All(c => c.Udid != d.Udid)).ToList())
        {
            gone.Lifetime.Cancel();
            Devices.Remove(gone);
        }

        foreach (var device in current.Where(c => Devices.All(d => d.Udid != c.Udid)))
        {
            var item = new DeviceItemViewModel(device.Udid, PrepareAsync);
            Devices.Add(item);
            _ = PrepareAsync(item);
        }

        for (var i = 0; i < Devices.Count; i++)
            Devices[i].ShowSeparator = i > 0;
        Count = Devices.Count;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task PrepareAsync(DeviceItemViewModel item)
    {
        var token = item.Lifetime.Token;
        item.ErrorText = null;
        item.Status = DeviceStatus.Connecting;
        StateChanged?.Invoke(this, EventArgs.Empty);

        // A freshly plugged-in phone can take a moment before lockdownd answers.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var info = await _devices.GetInfoAsync(item.Udid, token);
                item.Apply(info);

                if (info.Trust != DeviceTrust.Trusted)
                {
                    item.Status = DeviceStatus.WaitingForTrust;
                    var progress = new Progress<DeviceTrust>(t =>
                        item.Status = t == DeviceTrust.Locked ? DeviceStatus.Locked : DeviceStatus.WaitingForTrust);
                    if (!await _devices.TrustAsync(item.Udid, progress, token))
                    {
                        item.Status = DeviceStatus.TrustDenied;
                        break;
                    }

                    info = await _devices.GetInfoAsync(item.Udid, token);
                    item.Apply(info);
                }

                item.Status = info.Trust == DeviceTrust.Trusted ? DeviceStatus.Ready : DeviceStatus.WaitingForTrust;
                break;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (DeviceOperationException ex) when (ex.Kind is DeviceErrorKind.Disconnected or DeviceErrorKind.Unknown && attempt < 3)
            {
                AppLog.Debug($"Device not ready yet ({ex.Message}); retrying.");
                await Task.Delay(1500, CancellationToken.None);
                if (token.IsCancellationRequested)
                    return;
            }
            catch (DeviceOperationException ex) when (ex.Kind == DeviceErrorKind.Locked)
            {
                item.Status = DeviceStatus.Locked;
                await Task.Delay(2000, CancellationToken.None);
                if (token.IsCancellationRequested)
                    return;
            }
            catch (DeviceOperationException ex)
            {
                AppLog.Warn($"Could not prepare {DeviceWatcher.Redact(item.Udid)}: {ex.Kind} {ex.Message}");
                item.ErrorText = ex.Kind switch
                {
                    DeviceErrorKind.TrustDenied => Loc.Get("Device_Status_Denied"),
                    DeviceErrorKind.Disconnected => Loc.Get("Device_Status_Reconnect"),
                    _ => Loc.Get("Device_Status_Error"),
                };
                item.Status = ex.Kind == DeviceErrorKind.TrustDenied ? DeviceStatus.TrustDenied : DeviceStatus.Error;
                break;
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
