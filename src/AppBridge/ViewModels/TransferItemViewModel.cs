using CommunityToolkit.Mvvm.ComponentModel;
using AppBridge.Core.Devices;
using AppBridge.Core.Transfer;
using AppBridge.Services;
using Microsoft.UI.Xaml.Media;

namespace AppBridge.ViewModels;

public sealed partial class TransferItemViewModel(InstalledApp app, ImageSource? icon, Flow flow = Flow.Now) : ObservableObject
{
    public InstalledApp App { get; } = app;

    public Flow Flow { get; } = flow;

    public string Name => App.Name;

    public string Initial => string.IsNullOrEmpty(App.Name) ? "?" : App.Name[..1].ToUpperInvariant();

    public ImageSource? Icon { get; } = icon;

    public bool HasIcon => Icon is not null;

    public bool ShowInitial => Icon is null;

    [ObservableProperty]
    public partial bool ShowSeparator { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(ShowProgress), nameof(IsIndeterminate), nameof(IsCompleted), nameof(IsFailed), nameof(IsActive), nameof(IsWaiting))]
    public partial TransferStage Stage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(IsIndeterminate), nameof(ProgressValue))]
    public partial double? Fraction { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial long BytesReceived { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial TransferFailure Failure { get; set; }

    [ObservableProperty]
    public partial string? Detail { get; set; }

    public double ProgressValue => (Fraction ?? 0) * 100;

    public bool IsCompleted => Stage == TransferStage.Completed;

    public bool IsFailed => Stage is TransferStage.Failed;

    public bool IsWaiting => Stage is TransferStage.Waiting or TransferStage.ReadyToInstall or TransferStage.Canceled;

    public bool IsActive => Stage is TransferStage.Downloading or TransferStage.Preparing or TransferStage.Uploading or TransferStage.Installing;

    public bool ShowProgress => IsActive;

    public bool IsIndeterminate => Stage == TransferStage.Preparing || (IsActive && Fraction is null);

    /// <summary>Weight of this item in the overall progress, 0..1: saving is all download, installing a saved app all USB.</summary>
    public double OverallFraction
    {
        get
        {
            var (download, upload) = Flow switch
            {
                Flow.Save => (0.95, 0.0),
                Flow.InstallSaved => (0.0, 0.7),
                _ => (0.6, 0.25),
            };
            return Stage switch
            {
                TransferStage.Completed or TransferStage.Failed => 1,
                TransferStage.Downloading => download * (Fraction ?? 0),
                TransferStage.Preparing or TransferStage.ReadyToInstall => download,
                TransferStage.Uploading => download + upload * (Fraction ?? 0),
                TransferStage.Installing => download + upload + (1 - download - upload) * (Fraction ?? 0),
                _ => 0,
            };
        }
    }

    public string StatusText => Stage switch
    {
        TransferStage.Waiting => Loc.Get("Transfer_Waiting"),
        TransferStage.Downloading when Fraction is { } f => Loc.Format("Transfer_DownloadingPercent", Math.Round(f * 100)),
        TransferStage.Downloading when BytesReceived > 0 => Loc.Format("Transfer_DownloadingBytes", Format.Bytes(BytesReceived)),
        TransferStage.Downloading => Loc.Get("Transfer_Downloading"),
        TransferStage.Preparing => Loc.Get("Transfer_Preparing"),
        TransferStage.ReadyToInstall => Loc.Get(Flow == Flow.InstallSaved ? "Transfer_Waiting" : "Transfer_ReadyToInstall"),
        TransferStage.Uploading => Loc.Format("Transfer_Uploading", Math.Round((Fraction ?? 0) * 100)),
        TransferStage.Installing => Loc.Get("Transfer_Installing"),
        TransferStage.Completed => Loc.Get(Flow == Flow.Save ? "Transfer_Saved" : "Transfer_Completed"),
        TransferStage.Canceled => Loc.Get("Transfer_NotStarted"),
        TransferStage.Failed => FailureText(Failure),
        _ => "",
    };

    public static string FailureText(TransferFailure failure) => failure switch
    {
        TransferFailure.NotSignedIn => Loc.Get("Failure_NotSignedIn"),
        TransferFailure.NotPurchased => Loc.Get("Failure_NotPurchased"),
        TransferFailure.PaidApp => Loc.Get("Failure_PaidApp"),
        TransferFailure.NotAvailable => Loc.Get("Failure_NotAvailable"),
        TransferFailure.Network => Loc.Get("Failure_Network"),
        TransferFailure.OsTooOld => Loc.Get("Failure_OsTooOld"),
        TransferFailure.NotEnoughSpace => Loc.Get("Failure_NotEnoughSpace"),
        TransferFailure.DeviceDisconnected => Loc.Get("Failure_DeviceDisconnected"),
        TransferFailure.NotTrusted => Loc.Get("Failure_NotTrusted"),
        TransferFailure.DeviceLocked => Loc.Get("Failure_DeviceLocked"),
        TransferFailure.InstallFailed => Loc.Get("Failure_InstallFailed"),
        TransferFailure.FileMissing => Loc.Get("Failure_FileMissing"),
        TransferFailure.FileDamaged => Loc.Get("Failure_FileDamaged"),
        _ => Loc.Get("Failure_Unknown"),
    };

    public override string ToString() => $"{Name}, {StatusText}";

    public void Apply(TransferUpdate update)
    {
        Stage = update.Stage;
        Fraction = update.Fraction;
        BytesReceived = update.BytesReceived;
        Failure = update.Failure;
        Detail = update.Detail;
    }
}
