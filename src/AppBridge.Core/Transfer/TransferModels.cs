namespace AppBridge.Core.Transfer;

public enum TransferStage
{
    Waiting,
    Downloading,
    Preparing,
    ReadyToInstall,
    Uploading,
    Installing,
    Completed,
    Failed,
    Canceled,
}

public enum TransferFailure
{
    None,
    NotSignedIn,
    NotPurchased,
    PaidApp,
    NotAvailable,
    Network,
    OsTooOld,
    NotEnoughSpace,
    DeviceDisconnected,
    NotTrusted,
    DeviceLocked,
    InstallFailed,

    /// <summary>A saved app's file was moved or deleted since the list was read.</summary>
    FileMissing,

    /// <summary>A saved app's file can't be read or holds a different app.</summary>
    FileDamaged,
    Unknown,
}

public readonly record struct TransferUpdate(
    TransferStage Stage,
    double? Fraction = null,
    long BytesReceived = 0,
    TransferFailure Failure = TransferFailure.None,
    string? Detail = null);

public sealed record TransferOptions(
    string DownloadDirectory,
    bool KeepDownloads,
    VersionPolicy VersionPolicy,
    bool AcquireFreeLicenses);
