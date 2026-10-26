using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Channels;
using FreeReturn.Core.Devices;
using FreeReturn.Core.Ipatool;

namespace FreeReturn.Core.Transfer;

/// <summary>
/// Downloads each selected app from the App Store and installs it on the target iPhone.
/// The next app downloads while the previous one installs. Problems that would make every
/// remaining app fail (signed out, phone unplugged or untrusted) stop the whole run.
/// </summary>
public sealed class TransferRunner(IAppStoreClient ipatool, IDeviceService devices)
{
    // Survives between runs so "Retry" does not download the same package twice.
    private readonly ConcurrentDictionary<string, string> _downloaded = new();

    public async Task RunAsync(
        IReadOnlyList<InstalledApp> apps,
        string targetUdid,
        TransferOptions options,
        Action<InstalledApp, TransferUpdate> report,
        CancellationToken cancellationToken)
    {
        using var abort = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = abort.Token;
        var finished = new ConcurrentDictionary<string, bool>();
        var ready = Channel.CreateBounded<(InstalledApp App, string Path)>(new BoundedChannelOptions(1) { SingleReader = true, SingleWriter = true });

        void Finish(InstalledApp app, TransferUpdate update)
        {
            finished[app.BundleId] = true;
            report(app, update);
        }

        void Fail(InstalledApp app, TransferFailure failure, string detail, bool fatal)
        {
            AppLog.Warn($"{app.BundleId}: {failure} - {detail}");
            Finish(app, new TransferUpdate(TransferStage.Failed, Failure: failure, Detail: detail));
            if (fatal)
                abort.Cancel();
        }

        foreach (var app in apps)
            report(app, new TransferUpdate(TransferStage.Waiting));

        var downloader = Task.Run(async () =>
        {
            try
            {
                foreach (var app in apps)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var path = await DownloadAsync(app, options, update => report(app, update), token).ConfigureAwait(false);
                        report(app, new TransferUpdate(TransferStage.ReadyToInstall));
                        await ready.Writer.WriteAsync((app, path), token).ConfigureAwait(false);
                    }
                    catch (IpatoolException ex)
                    {
                        var (failure, fatal) = Map(ex.Kind);
                        Fail(app, failure, ex.Message, fatal);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        AppLog.Error($"{app.BundleId}: download failed unexpectedly.", ex);
                        Fail(app, TransferFailure.Unknown, ex.Message, fatal: false);
                    }
                }
            }
            finally
            {
                ready.Writer.TryComplete();
            }
        }, CancellationToken.None);

        var installer = Task.Run(async () =>
        {
            await foreach (var (app, path) in ready.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                try
                {
                    var progress = new InlineProgress<InstallProgress>(p => report(app, new TransferUpdate(
                        p.Phase == InstallPhase.Uploading ? TransferStage.Uploading : TransferStage.Installing, p.Fraction)));
                    await devices.InstallAsync(targetUdid, path, progress, token).ConfigureAwait(false);
                    Finish(app, new TransferUpdate(TransferStage.Completed, 1));
                    if (!options.KeepDownloads)
                        DeleteDownload(path);
                }
                catch (DeviceOperationException ex)
                {
                    var (failure, fatal) = Map(ex.Kind);
                    Fail(app, failure, ex.Message, fatal);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    AppLog.Error($"{app.BundleId}: installation failed unexpectedly.", ex);
                    Fail(app, TransferFailure.InstallFailed, ex.Message, fatal: false);
                }
            }
        }, CancellationToken.None);

        try
        {
            await Task.WhenAll(downloader, installer).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            foreach (var app in apps.Where(a => !finished.ContainsKey(a.BundleId)))
                report(app, new TransferUpdate(TransferStage.Canceled));
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static string PackageFileName(InstalledApp app, string? externalVersionId)
    {
        var safeBundle = string.Concat(app.BundleId.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));
        var id = (app.AppStoreId ?? 0).ToString(CultureInfo.InvariantCulture);
        return externalVersionId is null ? $"{safeBundle}_{id}.ipa" : $"{safeBundle}_{id}_v{externalVersionId}.ipa";
    }

    private async Task<string> DownloadAsync(InstalledApp app, TransferOptions options, Action<TransferUpdate> report, CancellationToken token)
    {
        var externalVersion = options.VersionPolicy == VersionPolicy.SameAsSource ? app.ExternalVersionId : null;
        var target = Path.Combine(options.DownloadDirectory, PackageFileName(app, externalVersion));
        var cacheKey = target.ToUpperInvariant();

        // An exact version never changes, so an existing valid file can be reused.
        if ((externalVersion is not null || _downloaded.ContainsKey(cacheKey)) && IsValidPackage(target, app.BundleId))
        {
            AppLog.Info($"{app.BundleId}: using the package already downloaded.");
            return target;
        }

        report(new TransferUpdate(TransferStage.Downloading));
        var progress = new InlineProgress<DownloadProgress>(p => report(p.Phase == DownloadPhase.Finalizing
            ? new TransferUpdate(TransferStage.Preparing, null, p.BytesReceived)
            : new TransferUpdate(TransferStage.Downloading, p.Fraction, p.BytesReceived)));

        var request = new DownloadRequest(app.AppStoreId ?? 0, app.BundleId, target, externalVersion, options.AcquireFreeLicenses);
        AppLog.Info($"{app.BundleId}: downloading{(externalVersion is null ? "" : $" version {externalVersion}")}.");
        var path = await ipatool.DownloadAsync(request, progress, token).ConfigureAwait(false);

        if (!IsValidPackage(path, app.BundleId))
            throw new IpatoolException(IpatoolErrorKind.Unknown, "The downloaded package is not the expected app.");
        _downloaded[cacheKey] = path;
        return path;
    }

    private static bool IsValidPackage(string path, string bundleId)
    {
        if (!File.Exists(path))
            return false;
        try
        {
            return string.Equals(IpaPackage.Read(path).BundleId, bundleId, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or Netimobiledevice.Plist.PlistFormatException)
        {
            AppLog.Warn($"Ignoring unreadable package {Path.GetFileName(path)}: {ex.Message}");
            return false;
        }
    }

    private void DeleteDownload(string path)
    {
        try
        {
            File.Delete(path);
            _downloaded.TryRemove(path.ToUpperInvariant(), out _);
        }
        catch (IOException ex)
        {
            AppLog.Warn($"Could not delete {Path.GetFileName(path)}.", ex);
        }
    }

    private static (TransferFailure Failure, bool Fatal) Map(IpatoolErrorKind kind) => kind switch
    {
        IpatoolErrorKind.NotSignedIn or IpatoolErrorKind.SessionExpired or IpatoolErrorKind.ToolMissing => (TransferFailure.NotSignedIn, true),
        IpatoolErrorKind.LicenseRequired => (TransferFailure.NotPurchased, false),
        IpatoolErrorKind.PaidAppNotSupported or IpatoolErrorKind.SubscriptionRequired => (TransferFailure.PaidApp, false),
        IpatoolErrorKind.AppNotFound or IpatoolErrorKind.TemporarilyUnavailable => (TransferFailure.NotAvailable, false),
        IpatoolErrorKind.Network => (TransferFailure.Network, false),
        _ => (TransferFailure.Unknown, false),
    };

    private static (TransferFailure Failure, bool Fatal) Map(DeviceErrorKind kind) => kind switch
    {
        DeviceErrorKind.Disconnected or DeviceErrorKind.ServiceUnavailable => (TransferFailure.DeviceDisconnected, true),
        DeviceErrorKind.NotTrusted or DeviceErrorKind.TrustDenied => (TransferFailure.NotTrusted, true),
        DeviceErrorKind.Locked => (TransferFailure.DeviceLocked, true),
        DeviceErrorKind.OsTooOld => (TransferFailure.OsTooOld, false),
        DeviceErrorKind.NotEnoughSpace => (TransferFailure.NotEnoughSpace, true),
        DeviceErrorKind.InstallFailed => (TransferFailure.InstallFailed, false),
        _ => (TransferFailure.Unknown, false),
    };

    private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
