using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppBridge.Core.Devices;
using AppBridge.Services;

namespace AppBridge.ViewModels;

public enum DeviceStatus
{
    Connecting,
    WaitingForTrust,
    Locked,
    TrustDenied,
    Ready,
    Error,
}

public sealed partial class DeviceItemViewModel : ObservableObject
{
    private readonly Func<DeviceItemViewModel, Task> _prepare;

    public DeviceItemViewModel(string udid, Func<DeviceItemViewModel, Task> prepare)
    {
        Udid = udid;
        _prepare = prepare;
    }

    public string Udid { get; }

    internal CancellationTokenSource Lifetime { get; } = new();

    [ObservableProperty]
    public partial string Name { get; set; } = "iPhone";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    public partial string Model { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    public partial string OsVersion { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    public partial long? FreeBytes { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(IsBusy), nameof(IsReady), nameof(CanRetry), nameof(ShowStatusIcon), nameof(StatusGlyph), nameof(IsSelectable))]
    public partial DeviceStatus Status { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial string? ErrorText { get; set; }

    /// <summary>Shows the blue checkmark for the device chosen in this step.</summary>
    [ObservableProperty]
    public partial bool IsChosen { get; set; }

    [ObservableProperty]
    public partial bool ShowSeparator { get; set; }

    /// <summary>In the target step, the source iPhone is listed but cannot be picked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectable), nameof(RoleText), nameof(HasRole))]
    public partial bool IsSourceDevice { get; set; }

    public string Subtitle
    {
        get
        {
            List<string> parts = [];
            if (!string.IsNullOrEmpty(Model))
                parts.Add(Model);
            if (!string.IsNullOrEmpty(OsVersion))
                parts.Add(Loc.Format("Device_IosVersion", OsVersion));
            if (FreeBytes is { } free)
                parts.Add(Loc.Format("Device_FreeSpace", Format.Bytes(free)));
            return string.Join("  ·  ", parts);
        }
    }

    public string StatusText => Status switch
    {
        DeviceStatus.Connecting => Loc.Get("Device_Status_Connecting"),
        DeviceStatus.WaitingForTrust => Loc.Get("Device_Status_TapTrust"),
        DeviceStatus.Locked => Loc.Get("Device_Status_Unlock"),
        DeviceStatus.TrustDenied => Loc.Get("Device_Status_Denied"),
        DeviceStatus.Error => ErrorText ?? Loc.Get("Device_Status_Error"),
        _ => "",
    };

    public bool IsBusy => Status is DeviceStatus.Connecting or DeviceStatus.WaitingForTrust or DeviceStatus.Locked;

    public bool IsReady => Status == DeviceStatus.Ready;

    public bool CanRetry => Status is DeviceStatus.TrustDenied or DeviceStatus.Error;

    public bool ShowStatusIcon => Status is DeviceStatus.TrustDenied or DeviceStatus.Error;

    public string StatusGlyph => "";

    public bool IsSelectable => IsReady && !IsSourceDevice;

    public bool HasRole => IsSourceDevice;

    public string RoleText => IsSourceDevice ? Loc.Get("Device_Role_Source") : "";

    [RelayCommand]
    private Task RetryAsync() => _prepare(this);

    public override string ToString() => string.IsNullOrEmpty(StatusText) ? $"{Name}, {Subtitle}" : $"{Name}, {StatusText}";

    internal void Apply(DeviceInfo info)
    {
        Name = info.Name;
        Model = info.ModelName;
        OsVersion = info.OsVersion;
        FreeBytes = info.FreeBytes;
    }
}
