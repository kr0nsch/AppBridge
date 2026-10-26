using System.Globalization;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Netimobiledevice;
using Netimobiledevice.Afc;
using Netimobiledevice.Exceptions;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Pairing;
using Netimobiledevice.Plist;
using Netimobiledevice.SpringBoardServices;
using Netimobiledevice.Usbmuxd;

namespace FreeReturn.Core.Devices;

/// <summary>Talks to iPhones over USB through the Apple Mobile Device service (lockdownd).</summary>
public sealed class DeviceService(string pairRecordsDirectory) : IDeviceService
{
    private const string ClientLabel = "FreeReturn";
    private const int UploadChunkSize = 256 * 1024;

    private static readonly string[] AppAttributes =
    [
        "CFBundleIdentifier", "CFBundleDisplayName", "CFBundleName", "CFBundleShortVersionString",
        "CFBundleVersion", "MinimumOSVersion", "StaticDiskUsage", "iTunesMetadata",
    ];

    private readonly ILogger _logger = AppLog.CreateLibraryLogger("Netimobiledevice");

    public Task<DeviceInfo> GetInfoAsync(string udid, CancellationToken cancellationToken = default) => RunAsync(() =>
    {
        using var lockdown = Connect(udid);
        var trusted = lockdown.IsPaired;
        long? capacity = null, free = null;
        if (trusted)
        {
            var usage = TryGet(() => lockdown.GetValue("com.apple.disk_usage", null)) as DictionaryNode;
            capacity = GetLong(usage, "TotalDataCapacity") ?? GetLong(usage, "TotalDiskCapacity");
            free = GetLong(usage, "TotalDataAvailable");
        }

        return new DeviceInfo(
            udid,
            TryGet(() => lockdown.DeviceName) is { Length: > 0 } name ? name : "iPhone",
            lockdown.ProductType,
            TryGet(() => lockdown.ProductFriendlyName) is { Length: > 0 } model ? model : lockdown.ProductType,
            TryGet(() => lockdown.OsVersion.ToString()) ?? "",
            trusted ? DeviceTrust.Trusted : DeviceTrust.NotTrusted,
            capacity,
            free);
    }, cancellationToken);

    /// <summary>
    /// Asks the iPhone to trust this PC and waits until the user answers. Progress reports
    /// <see cref="DeviceTrust.Locked"/> while the phone must be unlocked first.
    /// </summary>
    public Task<bool> TrustAsync(string udid, IProgress<DeviceTrust>? progress, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        using var lockdown = Connect(udid);
        if (lockdown.IsPaired)
            return true;

        AppLog.Info($"Requesting trust from {DeviceWatcher.Redact(udid)}");
        var states = new InlineProgress<PairingState>(state =>
        {
            if (state == PairingState.PasswordProtected)
                progress?.Report(DeviceTrust.Locked);
            else if (state == PairingState.PairingDialogResponsePending)
                progress?.Report(DeviceTrust.NotTrusted);
        });
        var paired = await lockdown.PairAsync(states, cancellationToken).ConfigureAwait(false);
        AppLog.Info(paired ? "The iPhone trusted this PC." : "The iPhone declined to trust this PC.");
        return paired;
    }, cancellationToken);

    public Task<IReadOnlyList<InstalledApp>> GetUserAppsAsync(string udid, CancellationToken cancellationToken = default) => RunAsync<IReadOnlyList<InstalledApp>>(async () =>
    {
        using var lockdown = ConnectTrusted(udid);
        using var proxy = new InstallationProxyClient(lockdown, _logger);
        var raw = await proxy.BrowseUserAppsAsync(AppAttributes, cancellationToken).ConfigureAwait(false);
        var apps = raw.Select(ParseApp).OfType<InstalledApp>()
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        AppLog.Info($"Read {apps.Count} apps from {DeviceWatcher.Redact(udid)} ({apps.Count(a => a.IsFromAppStore)} from the App Store).");
        return apps;
    }, cancellationToken);

    /// <summary>Fetches home-screen icons one by one; <paramref name="onIcon"/> runs on a background thread.</summary>
    public Task LoadIconsAsync(string udid, IReadOnlyList<string> bundleIds, Action<string, byte[]> onIcon, CancellationToken cancellationToken = default) => RunAsync(() =>
    {
        using var lockdown = ConnectTrusted(udid);
        using var springBoard = new SpringBoardServicesService(lockdown, _logger);
        foreach (var bundleId in bundleIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var png = springBoard.GetIconPNGData(bundleId).Value;
                if (png.Length > 0)
                    onIcon(bundleId, png);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AppLog.Debug($"No icon for {bundleId}: {ex.Message}");
            }
        }

        return true;
    }, cancellationToken);

    public Task InstallAsync(string udid, string ipaPath, IProgress<InstallProgress>? progress, CancellationToken cancellationToken = default) => RunAsync(async () =>
    {
        var package = IpaPackage.Read(ipaPath);
        using var lockdown = ConnectTrusted(udid);

        if (package.MinimumOsVersion is { } minimum && TryParseVersion(minimum, out var required)
            && TryParseVersion(TryGet(() => lockdown.OsVersion.ToString()) ?? "", out var actual) && actual < required)
            throw new DeviceOperationException(DeviceErrorKind.OsTooOld, $"Requires iOS {minimum} or later.");

        var remotePath = $"/FreeReturn-{Guid.NewGuid():N}.ipa";
        try
        {
            using (var afc = new AfcService(lockdown, logger: _logger))
            {
                var handle = await afc.FileOpen(remotePath, cancellationToken, AfcFileOpenMode.WriteOnly).ConfigureAwait(false);
                try
                {
                    await using var file = new FileStream(ipaPath, FileMode.Open, FileAccess.Read, FileShare.Read, UploadChunkSize, useAsync: true);
                    var total = Math.Max(1, file.Length);
                    var buffer = new byte[UploadChunkSize];
                    long sent = 0;
                    int read;
                    while ((read = await file.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        var chunk = read == buffer.Length ? buffer : buffer[..read];
                        await afc.FileWrite(handle, chunk, cancellationToken, chunk.Length).ConfigureAwait(false);
                        sent += read;
                        progress?.Report(new InstallProgress(InstallPhase.Uploading, (double)sent / total));
                    }
                }
                finally
                {
                    await afc.FileClose(handle, CancellationToken.None).ConfigureAwait(false);
                }
            }

            progress?.Report(new InstallProgress(InstallPhase.Installing, 0));
            using var proxy = new InstallationProxyClient(lockdown, _logger);
            await proxy.InstallAsync(remotePath, package,
                percent => progress?.Report(new InstallProgress(InstallPhase.Installing, percent / 100.0)),
                cancellationToken).ConfigureAwait(false);
            AppLog.Info($"Installed {package.BundleId} {package.Version} on {DeviceWatcher.Redact(udid)}.");
        }
        finally
        {
            await TryRemoveAsync(lockdown, remotePath).ConfigureAwait(false);
        }

        return true;
    }, cancellationToken);

    internal static InstalledApp? ParseApp(DictionaryNode app)
    {
        var bundleId = GetString(app, "CFBundleIdentifier");
        if (string.IsNullOrEmpty(bundleId))
            return null;

        DictionaryNode? metadata = null;
        if (app.TryGetValue("iTunesMetadata", out var node) && node is DataNode data && data.Value.Length > 0)
        {
            try
            {
                metadata = PropertyList.LoadFromByteArray(data.Value) as DictionaryNode;
            }
            catch (Exception ex)
            {
                AppLog.Debug($"Unreadable App Store metadata for {bundleId}: {ex.Message}");
            }
        }

        var accountInfo = (metadata is not null && metadata.TryGetValue("com.apple.iTunesStore.downloadInfo", out var download) && download is DictionaryNode downloadInfo
            && downloadInfo.TryGetValue("accountInfo", out var account)) ? account as DictionaryNode : null;

        var name = GetString(app, "CFBundleDisplayName") is { Length: > 0 } display ? display
            : GetString(app, "CFBundleName") is { Length: > 0 } bundleName ? bundleName
            : GetString(metadata, "itemName") ?? bundleId;

        var externalVersion = GetLong(metadata, "softwareVersionExternalIdentifier");
        return new InstalledApp(
            bundleId,
            name,
            GetString(app, "CFBundleShortVersionString") ?? GetString(metadata, "bundleShortVersionString") ?? "",
            GetString(metadata, "artistName"),
            GetLong(metadata, "itemId"),
            externalVersion is > 0 ? externalVersion.Value.ToString(CultureInfo.InvariantCulture) : null,
            GetString(accountInfo, "AppleID") ?? GetString(metadata, "apple-id"),
            GetString(app, "MinimumOSVersion"),
            GetLong(app, "StaticDiskUsage"));
    }

    internal static bool TryParseVersion(string text, out Version version)
    {
        var parts = text.Split('.', StringSplitOptions.RemoveEmptyEntries).Take(4).ToList();
        while (parts.Count < 2)
            parts.Add("0");
        return Version.TryParse(string.Join('.', parts), out version!);
    }

    private UsbmuxLockdownClient Connect(string udid)
    {
        Directory.CreateDirectory(pairRecordsDirectory);
        return MobileDevice.CreateUsingUsbmux(
            udid,
            label: ClientLabel,
            autopair: false,
            connectionType: UsbmuxdConnectionType.Usb,
            pairingRecordsCacheDir: pairRecordsDirectory,
            logger: _logger);
    }

    private UsbmuxLockdownClient ConnectTrusted(string udid)
    {
        var lockdown = Connect(udid);
        if (lockdown.IsPaired)
            return lockdown;
        lockdown.Dispose();
        throw new DeviceOperationException(DeviceErrorKind.NotTrusted, "This PC is not trusted by the iPhone.");
    }

    private async Task TryRemoveAsync(LockdownServiceProvider lockdown, string remotePath)
    {
        try
        {
            using var afc = new AfcService(lockdown, logger: _logger);
            await afc.Rm(remotePath, CancellationToken.None, force: true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLog.Debug($"Staged package cleanup skipped: {ex.Message}");
        }
    }

    private static Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken) =>
        RunAsync(() => Task.FromResult(work()), cancellationToken);

    private static async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(work, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (Translate(ex) is { } translated)
        {
            throw translated;
        }
    }

    private static DeviceOperationException? Translate(Exception ex) => ex switch
    {
        OperationCanceledException or DeviceOperationException => null,
        NotPairedException => new(DeviceErrorKind.NotTrusted, "This PC is not trusted by the iPhone.", ex),
        PasswordRequiredException => new(DeviceErrorKind.Locked, "Unlock the iPhone and try again.", ex),
        LockdownException { LockdownError: LockdownError.PasswordProtected } => new(DeviceErrorKind.Locked, "Unlock the iPhone and try again.", ex),
        LockdownException { LockdownError: LockdownError.UserDeniedPairing } => new(DeviceErrorKind.TrustDenied, "The iPhone declined to trust this PC.", ex),
        LockdownException { LockdownError: LockdownError.InvalidHostID or LockdownError.InvalidPairRecord or LockdownError.MissingPairRecord }
            => new(DeviceErrorKind.NotTrusted, "This PC is not trusted by the iPhone.", ex),
        UsbmuxConnectionException => new(DeviceErrorKind.ServiceUnavailable, "The Apple Mobile Device service is not running.", ex),
        NoDeviceConnectedException or DeviceDisconnectedException or SocketException or IOException or ObjectDisposedException
            => new(DeviceErrorKind.Disconnected, "The iPhone was disconnected.", ex),
        InvalidDataException => new(DeviceErrorKind.InstallFailed, ex.Message, ex),
        _ => new(DeviceErrorKind.Unknown, ex.Message, ex),
    };

    private static T? TryGet<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return default;
        }
    }

    private static string? GetString(DictionaryNode? dict, string key) =>
        dict is not null && dict.TryGetValue(key, out var node) && node is StringNode s ? s.Value : null;

    private static long? GetLong(DictionaryNode? dict, string key) =>
        dict is not null && dict.TryGetValue(key, out var node) ? node switch
        {
            IntegerNode i => i.SignedValue,
            StringNode s when long.TryParse(s.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) => v,
            _ => null,
        } : null;

    /// <summary>Reports synchronously, unlike <see cref="Progress{T}"/> which posts to a captured context.</summary>
    private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
