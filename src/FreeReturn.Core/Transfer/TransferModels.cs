namespace FreeReturn.Core.Transfer;

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
