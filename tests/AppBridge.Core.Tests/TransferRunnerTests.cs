using System.Collections.Concurrent;
using AppBridge.Core.Devices;
using AppBridge.Core.Ipatool;
using AppBridge.Core.Library;
using AppBridge.Core.Transfer;

namespace AppBridge.Core.Tests;

public sealed class TransferRunnerTests : IDisposable
{
    private readonly string _downloads = TestPaths.TempDirectory();
    private readonly FakeStore _store = new();
    private readonly FakeDevices _devices = new();
    private readonly ConcurrentDictionary<string, TransferUpdate> _final = new();

    public void Dispose() => Directory.Delete(_downloads, true);

    [Fact]
    public async Task InstallsEveryApp()
    {
        var apps = new[] { App("a"), App("b"), App("c") };

        await Run(apps);

        Assert.All(apps, a => Assert.Equal(TransferStage.Completed, _final[a.BundleId].Stage));
        Assert.Equal(["a", "b", "c"], _devices.Installed);
    }

    [Fact]
    public async Task NonFatalFailureDoesNotStopOthers()
    {
        _store.Failures["b"] = IpatoolErrorKind.LicenseRequired;

        await Run([App("a"), App("b"), App("c")]);

        Assert.Equal(TransferStage.Completed, _final["a"].Stage);
        Assert.Equal(TransferFailure.NotPurchased, _final["b"].Failure);
        Assert.Equal(TransferStage.Completed, _final["c"].Stage);
    }

    [Fact]
    public async Task UnpluggedPhoneStopsTheRun()
    {
        _devices.Failures["b"] = DeviceErrorKind.Disconnected;

        await Run([App("a"), App("b"), App("c"), App("d")]);

        Assert.Equal(TransferStage.Completed, _final["a"].Stage);
        Assert.Equal(TransferFailure.DeviceDisconnected, _final["b"].Failure);
        Assert.Equal(TransferStage.Canceled, _final["d"].Stage);
        Assert.DoesNotContain("d", _devices.Installed);
    }

    [Fact]
    public async Task SignedOutStopsTheRun()
    {
        _store.Failures["a"] = IpatoolErrorKind.NotSignedIn;

        await Run([App("a"), App("b")]);

        Assert.Equal(TransferFailure.NotSignedIn, _final["a"].Failure);
        Assert.Equal(TransferStage.Canceled, _final["b"].Stage);
        Assert.Single(_store.Requests);
    }

    [Fact]
    public async Task ExactVersionIsDownloadedOnceAndReused()
    {
        var app = App("a");
        var runner = new TransferRunner(_store, _devices);

        await Run([app], runner, VersionPolicy.SameAsSource);
        await Run([app], runner, VersionPolicy.SameAsSource);

        Assert.Single(_store.Requests);
        Assert.Equal("ext-a", _store.Requests.Single().ExternalVersionId);
        Assert.Equal(2, _devices.Installed.Count);
    }

    [Fact]
    public async Task LatestVersionIsReusedOnRetryWithinSession()
    {
        var app = App("a");
        var runner = new TransferRunner(_store, _devices);

        await Run([app], runner);
        await Run([app], runner);

        Assert.Single(_store.Requests);
        Assert.Null(_store.Requests.Single().ExternalVersionId);
    }

    [Fact]
    public async Task DownloadsAreDeletedWhenNotKept()
    {
        await Run([App("a")], keep: false);

        Assert.Empty(Directory.GetFiles(_downloads, "*.ipa"));
    }

    [Fact]
    public async Task WrongPackageIsRejected()
    {
        _store.WrongBundle.Add("a");

        await Run([App("a")]);

        Assert.Equal(TransferStage.Failed, _final["a"].Stage);
        Assert.Empty(_devices.Installed);
    }

    [Fact]
    public async Task CancellationMarksRemainingAppsCanceled()
    {
        using var cts = new CancellationTokenSource();
        _devices.OnInstall = bundle =>
        {
            if (bundle == "a")
                cts.Cancel();
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run([App("a"), App("b")], token: cts.Token));

        Assert.Equal(TransferStage.Canceled, _final["b"].Stage);
    }

    [Fact]
    public async Task SavingDownloadsWithReadableNamesAndInstallsNothing()
    {
        await new TransferRunner(_store, _devices).SaveAsync([App("a"), App("b")],
            new TransferOptions(_downloads, true, VersionPolicy.Latest, AcquireFreeLicenses: true),
            (app, update) => _final[app.BundleId] = update, CancellationToken.None);

        Assert.All(new[] { "a", "b" }, id => Assert.Equal(TransferStage.Completed, _final[id].Stage));
        Assert.Empty(_devices.Installed);
        Assert.Equal(["App a 1.2.3.ipa", "App b 1.2.3.ipa"], Directory.GetFiles(_downloads, "*.ipa").Select(Path.GetFileName).Order());
    }

    [Fact]
    public async Task SavedAppsInstallWithoutDownloading()
    {
        var saved = new[] { "a", "b", "c" }.Select(id => SavedAppLibrary.Read(TestIpa.Create(_downloads, id))).ToList();
        File.Delete(saved[1].FilePath);

        await new TransferRunner(_store, _devices).InstallSavedAsync(saved, "target", (app, update) => _final[app.BundleId] = update, CancellationToken.None);

        Assert.Empty(_store.Requests);
        Assert.Equal(["a", "c"], _devices.Installed);
        Assert.Equal(TransferFailure.FileMissing, _final["b"].Failure);
        Assert.True(File.Exists(saved[0].FilePath), "saved files are kept after installing");
    }

    [Fact]
    public async Task SavingNeverOverwritesAnotherSavedVersion()
    {
        // A file kept by 1.0 under the package name, and a saved file with the readable name.
        var old = TestIpa.Create(_downloads, "a", version: "0.9");
        File.Move(old, Path.Combine(_downloads, TransferRunner.PackageFileName(App("a"), null)));
        var clash = TestIpa.Create(_downloads, "other.app", version: "1.2.3");
        File.Move(clash, Path.Combine(_downloads, "App a 1.2.3.ipa"));

        await new TransferRunner(_store, _devices).SaveAsync([App("a")],
            new TransferOptions(_downloads, true, VersionPolicy.Latest, AcquireFreeLicenses: true),
            (app, update) => _final[app.BundleId] = update, CancellationToken.None);

        var versions = Directory.GetFiles(_downloads, "*.ipa").Select(f => (Path.GetFileName(f), IpaPackage.Read(f).BundleId, IpaPackage.Read(f).Version)).Order().ToList();
        Assert.Equal(
            [($"a_{100 + 'a'}.ipa", "a", "0.9"), ("App a (2) 1.2.3.ipa", "a", "1.2.3"), ("App a 1.2.3.ipa", "other.app", "1.2.3")],
            versions);
    }

    [Fact]
    public async Task TransferWithoutKeepingNeverDeletesSavedFiles()
    {
        var saved = SavedAppLibrary.Read(TestIpa.Create(_downloads, "a"));
        File.Move(saved.FilePath, Path.Combine(_downloads, TransferRunner.PackageFileName(App("a"), "ext-a")));

        await Run([App("a")], policy: VersionPolicy.SameAsSource, keep: false);

        Assert.Equal(TransferStage.Completed, _final["a"].Stage);
        Assert.Single(Directory.GetFiles(_downloads, "*.ipa"));
        Assert.Empty(Directory.GetFiles(Path.Combine(_downloads, TransferRunner.StagingFolderName)));
    }

    [Theory]
    [InlineData("Weather", "2.1", "com.example", "Weather 2.1.ipa")]
    [InlineData("A/B: C?", "1.0", "com.example", "A_B_ C_ 1.0.ipa")]
    [InlineData("...", "", "com.example", "com.example.ipa")]
    public void SavedNamesAreReadableAndSafe(string name, string version, string bundle, string expected) =>
        Assert.Equal(expected, TransferRunner.SavedFileName(name, version, bundle));

    [Theory]
    [InlineData("com.example.app", null, "com.example.app_42.ipa")]
    [InlineData("com.example.app", "999", "com.example.app_42_v999.ipa")]
    [InlineData("bad/name:with*chars", null, "bad_name_with_chars_42.ipa")]
    public void PackageNamesAreSafe(string bundle, string? version, string expected)
    {
        var app = new InstalledApp(bundle, "x", "1", null, 42, version, null, null, null);
        Assert.Equal(expected, TransferRunner.PackageFileName(app, version));
    }

    private Task Run(IReadOnlyList<InstalledApp> apps, TransferRunner? runner = null, VersionPolicy policy = VersionPolicy.Latest,
        bool keep = true, CancellationToken token = default) =>
        (runner ?? new TransferRunner(_store, _devices)).RunAsync(
            apps, "target", new TransferOptions(_downloads, keep, policy, AcquireFreeLicenses: true),
            (app, update) => _final[app.BundleId] = update, token);

    private static InstalledApp App(string id) => new(id, $"App {id}", "1.0", "Dev", 100 + id[0], $"ext-{id}", null, null, 1000);

    private sealed class FakeStore : IAppStoreClient
    {
        public ConcurrentDictionary<string, IpatoolErrorKind> Failures { get; } = new();

        public ConcurrentBag<string> WrongBundle { get; } = [];

        public ConcurrentQueue<DownloadRequest> Requests { get; } = new();

        public bool IsAvailable => true;

        public Task<IpatoolAccount?> GetAccountAsync(CancellationToken cancellationToken = default) => Task.FromResult<IpatoolAccount?>(new("a@b.c", "A"));

        public Task<LoginResult> LoginAsync(string email, string password, string? authCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Enqueue(request);
            if (Failures.TryGetValue(request.BundleId, out var kind))
                throw new IpatoolException(kind, kind.ToString());
            progress?.Report(new DownloadProgress(DownloadPhase.Downloading, 0.5, 10));
            var dir = Path.GetDirectoryName(request.OutputPath)!;
            var built = TestIpa.Create(dir, WrongBundle.Contains(request.BundleId) ? "other.app" : request.BundleId);
            File.Move(built, request.OutputPath, overwrite: true);
            return Task.FromResult(request.OutputPath);
        }
    }

    private sealed class FakeDevices : IDeviceService
    {
        private readonly ConcurrentQueue<string> _installed = new();

        public ConcurrentDictionary<string, DeviceErrorKind> Failures { get; } = new();

        public Action<string>? OnInstall { get; set; }

        public List<string> Installed => [.. _installed];

        public Task<DeviceInfo> GetInfoAsync(string udid, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> TrustAsync(string udid, IProgress<DeviceTrust>? progress, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<InstalledApp>> GetUserAppsAsync(string udid, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task LoadIconsAsync(string udid, IReadOnlyList<string> bundleIds, Action<string, byte[]> onIcon, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public async Task InstallAsync(string udid, string ipaPath, IProgress<InstallProgress>? progress, CancellationToken cancellationToken = default)
        {
            var bundle = IpaPackage.Read(ipaPath).BundleId;
            OnInstall?.Invoke(bundle);
            cancellationToken.ThrowIfCancellationRequested();
            if (Failures.TryGetValue(bundle, out var kind))
                throw new DeviceOperationException(kind, kind.ToString());
            progress?.Report(new InstallProgress(InstallPhase.Uploading, 1));
            await Task.Yield();
            _installed.Enqueue(bundle);
        }
    }
}
