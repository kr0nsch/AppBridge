using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Channels;
using AppBridge.Core.Devices;
using AppBridge.Core.Ipatool;
using AppBridge.Core.Library;

namespace AppBridge.Core.Transfer;

/// <summary>
/// Downloads each selected app from the App Store and installs it on the target iPhone.
/// The next app downloads while the previous one installs. Problems that would make every
/// remaining app fail (signed out, phone unplugged or untrusted) stop the whole run.
/// The same pipeline also saves apps for later (download only) and installs saved apps
/// (install only).
/// </summary>
public sealed class TransferRunner(IAppStoreClient ipatool, IDeviceService devices)
{
    /// <summary>
    /// Hidden folder inside the download folder where ipatool writes. Saved apps live next to
    /// it, so a download can never overwrite one (it may be the last copy of a removed app).
    /// </summary>
    public const string StagingFolderName = ".appbridge-downloads";

    // Survives between runs so "Retry" does not download the same package twice.
    // Key: bundle id and requested version; value: where that package is now.
    private readonly ConcurrentDictionary<string, string> _downloaded = new();

    public Task RunAsync(
        IReadOnlyList<InstalledApp> apps,
        string targetUdid,
        TransferOptions options,
        Action<InstalledApp, TransferUpdate> report,
        CancellationToken cancellationToken) =>
        RunCoreAsync(apps, async (app, update, token) =>
        {
            var path = await DownloadAsync(app, options, update, token).ConfigureAwait(false);
            return options.KeepDownloads ? KeepAsSaved(path, app, options.DownloadDirectory) : path;
        }, targetUdid, Staging(options.DownloadDirectory), report, cancellationToken);

    /// <summary>Downloads the apps into <see cref="TransferOptions.DownloadDirectory"/> to install later.</summary>
    public Task SaveAsync(
        IReadOnlyList<InstalledApp> apps,
        TransferOptions options,
        Action<InstalledApp, TransferUpdate> report,
        CancellationToken cancellationToken) =>
        RunCoreAsync(apps, async (app, update, token) =>
        {
            var path = await DownloadAsync(app, options, update, token).ConfigureAwait(false);
            return KeepAsSaved(path, app, options.DownloadDirectory);
        }, targetUdid: null, Staging(options.DownloadDirectory), report, cancellationToken);

    /// <summary>Installs apps saved earlier. Nothing is downloaded, so no Apple sign-in is needed.</summary>
    public Task InstallSavedAsync(
        IReadOnlyList<SavedApp> apps,
        string targetUdid,
        Action<InstalledApp, TransferUpdate> report,
        CancellationToken cancellationToken)
    {
        var files = apps.ToDictionary(a => a.BundleId, a => a.FilePath, StringComparer.Ordinal);
        return RunCoreAsync(apps.Select(a => a.ToInstalledApp()).ToList(), (app, _, _) =>
        {
            var path = files[app.BundleId];
            if (!File.Exists(path))
                throw new FileNotFoundException("The saved file is no longer there.", path);
            if (!IsValidPackage(path, app.BundleId))
                throw new InvalidDataException("The saved file is damaged or holds a different app.");
            return Task.FromResult(path);
        }, targetUdid, stagingDirectory: null, report, cancellationToken);
    }

    /// <summary>
    /// Gets each app's package with <paramref name="acquire"/> and installs it on <paramref name="targetUdid"/>;
    /// with no target the app is finished as soon as its package is ready. Packages still in
    /// <paramref name="stagingDirectory"/> after installing were not kept and are deleted.
    /// </summary>
    private async Task RunCoreAsync(
        IReadOnlyList<InstalledApp> apps,
        Func<InstalledApp, Action<TransferUpdate>, CancellationToken, Task<string>> acquire,
        string? targetUdid,
        string? stagingDirectory,
        Action<InstalledApp, TransferUpdate> report,
        CancellationToken cancellationToken)
    {
        if (stagingDirectory is not null)
            CleanStaging(stagingDirectory);
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
                        var path = await acquire(app, update => report(app, update), token).ConfigureAwait(false);
                        if (targetUdid is null)
                        {
                            Finish(app, new TransferUpdate(TransferStage.Completed, 1));
                            continue;
                        }

                        report(app, new TransferUpdate(TransferStage.ReadyToInstall));
                        await ready.Writer.WriteAsync((app, path), token).ConfigureAwait(false);
                    }
                    catch (IpatoolException ex)
                    {
                        var (failure, fatal) = Map(ex.Kind);
                        Fail(app, failure, ex.Message, fatal);
                    }
                    catch (FileNotFoundException ex)
                    {
                        Fail(app, TransferFailure.FileMissing, ex.Message, fatal: false);
                    }
                    catch (InvalidDataException ex)
                    {
                        Fail(app, TransferFailure.FileDamaged, ex.Message, fatal: false);
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
                    await devices.InstallAsync(targetUdid!, path, progress, token).ConfigureAwait(false);
                    Finish(app, new TransferUpdate(TransferStage.Completed, 1));
                    if (stagingDirectory is not null && IsInside(path, stagingDirectory))
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
        var cacheKey = $"{app.BundleId}|{externalVersion}";
        if (_downloaded.TryGetValue(cacheKey, out var known) && IsValidPackage(known, app.BundleId))
        {
            AppLog.Info($"{app.BundleId}: using the package already downloaded.");
            return known;
        }

        var staging = Staging(options.DownloadDirectory);
        var folder = Directory.CreateDirectory(staging);
        folder.Attributes |= FileAttributes.Hidden;
        var target = Path.Combine(staging, $"{Path.GetFileNameWithoutExtension(PackageFileName(app, externalVersion))}_{Guid.NewGuid():N}.ipa");

        report(new TransferUpdate(TransferStage.Downloading));
        var progress = new InlineProgress<DownloadProgress>(p => report(p.Phase == DownloadPhase.Finalizing
            ? new TransferUpdate(TransferStage.Preparing, null, p.BytesReceived)
            : new TransferUpdate(TransferStage.Downloading, p.Fraction, p.BytesReceived)));

        var request = new DownloadRequest(app.AppStoreId ?? 0, app.BundleId, target, externalVersion, options.AcquireFreeLicenses);
        AppLog.Info($"{app.BundleId}: downloading{(externalVersion is null ? "" : $" version {externalVersion}")}.");
        var path = await ipatool.DownloadAsync(request, progress, token).ConfigureAwait(false);

        if (!IsValidPackage(path, app.BundleId))
        {
            DeleteDownload(path);
            throw new IpatoolException(IpatoolErrorKind.Unknown, "The downloaded package is not the expected app.");
        }

        _downloaded[cacheKey] = path;
        return path;
    }

    private static string Staging(string downloadDirectory) => Path.Combine(downloadDirectory, StagingFolderName);

    private static bool IsInside(string path, string folder) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>Removes leftovers of stopped downloads, keeping packages a retry can still use.</summary>
    private void CleanStaging(string staging)
    {
        if (!Directory.Exists(staging))
            return;
        var keep = new HashSet<string>(_downloaded.Values.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(staging))
        {
            if (keep.Contains(Path.GetFullPath(file)))
                continue;
            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AppLog.Debug($"Could not remove {Path.GetFileName(file)}: {ex.Message}");
            }
        }
    }

    /// <summary>File name for an app saved to install later, readable in Explorer: "Name 1.2.3.ipa".</summary>
    internal static string SavedFileName(string name, string version, string bundleId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = string.Concat(name.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c)).Trim(' ', '.');
        if (clean.Length == 0)
            clean = bundleId;
        if (clean.Length > 80)
            clean = clean[..80].TrimEnd();
        return string.IsNullOrWhiteSpace(version) ? $"{clean}.ipa" : $"{clean} {version}.ipa";
    }

    /// <summary>
    /// Moves a fresh download out of staging under its readable name. Only a file holding the
    /// same app at the same version is ever replaced; any other file keeps its name.
    /// </summary>
    private string KeepAsSaved(string path, InstalledApp app, string folder)
    {
        var staging = Staging(folder);
        if (!IsInside(path, staging))
            return path;

        var version = IpaPackage.Read(path).Version;
        var target = Path.Combine(folder, SavedFileName(app.Name, version, app.BundleId));
        for (var n = 2; File.Exists(target) && !IsSamePackage(target, app.BundleId, version); n++)
            target = Path.Combine(folder, SavedFileName($"{app.Name} ({n})", version, app.BundleId));

        File.Move(path, target, overwrite: true);
        foreach (var (key, value) in _downloaded)
        {
            if (string.Equals(value, path, StringComparison.OrdinalIgnoreCase))
                _downloaded[key] = target;
        }

        return target;
    }

    private static bool IsSamePackage(string path, string bundleId, string version)
    {
        try
        {
            var package = IpaPackage.Read(path);
            return package.BundleId == bundleId && package.Version == version;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    private static bool IsValidPackage(string path, string bundleId)
    {
        if (!File.Exists(path))
            return false;
        try
        {
            return string.Equals(IpaPackage.Read(path).BundleId, bundleId, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
            foreach (var (key, value) in _downloaded)
            {
                if (string.Equals(value, path, StringComparison.OrdinalIgnoreCase))
                    _downloaded.TryRemove(key, out _);
            }
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
