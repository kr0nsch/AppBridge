namespace FreeReturn.Core.Devices;

/// <summary>A device reported by the Apple Mobile Device service over USB.</summary>
public sealed record ConnectedDevice(string Udid, ulong MuxId);

public enum DeviceTrust
{
    /// <summary>This PC is trusted by the device; all services are available.</summary>
    Trusted,

    /// <summary>The device has not trusted this PC yet.</summary>
    NotTrusted,

    /// <summary>The device must be unlocked before it can trust this PC.</summary>
    Locked,
}

public sealed record DeviceInfo(
    string Udid,
    string Name,
    string ProductType,
    string ModelName,
    string OsVersion,
    DeviceTrust Trust,
    long? CapacityBytes = null,
    long? FreeBytes = null);

public sealed record InstalledApp(
    string BundleId,
    string Name,
    string Version,
    string? Developer,
    long? AppStoreId,
    string? ExternalVersionId,
    string? PurchaserAppleId,
    string? MinimumOsVersion,
    long? SizeBytes)
{
    /// <summary>Only App Store apps carry the store identifiers needed to download them again.</summary>
    public bool IsFromAppStore => AppStoreId is > 0;
}

public enum InstallPhase
{
    Uploading,
    Installing,
}

public readonly record struct InstallProgress(InstallPhase Phase, double Fraction);

public sealed class DeviceOperationException(DeviceErrorKind kind, string message, Exception? inner = null) : Exception(message, inner)
{
    public DeviceErrorKind Kind { get; } = kind;
}

public enum DeviceErrorKind
{
    Unknown,
    ServiceUnavailable,
    Disconnected,
    NotTrusted,
    Locked,
    TrustDenied,
    InstallFailed,
    OsTooOld,
    NotEnoughSpace,
}
