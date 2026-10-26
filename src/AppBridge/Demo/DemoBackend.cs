using System.IO.Compression;
using System.Text;
using AppBridge.Core;
using AppBridge.Core.Devices;
using AppBridge.Core.Ipatool;

namespace AppBridge.Demo;

/// <summary>
/// Simulated iPhones and App Store, enabled with <c>--demo</c>. Used to develop the UI and to
/// take screenshots without hardware or an Apple Account. All names here are fictional.
/// </summary>
internal sealed class DemoBackend : IAppStoreClient, IDeviceService, IDeviceWatcher
{
    private const string SourceUdid = "00008110-DEMO0000000000A1";
    private const string TargetUdid = "00008140-DEMO0000000000B2";

    private static readonly (string Name, string Developer, string Bundle, long Size, bool Paid)[] Catalog =
    [
        ("Bloom Journal", "Petal Labs", "demo.bloom.journal", 182_000_000, false),
        ("Cloudy", "Northwind Apps", "demo.cloudy.weather", 96_400_000, false),
        ("Daily Planner", "Tidy Tools", "demo.daily.planner", 64_900_000, false),
        ("Echo Podcasts", "Riverbank Audio", "demo.echo.podcasts", 121_300_000, false),
        ("Fresh Recipes", "Kitchen Table", "demo.fresh.recipes", 210_700_000, false),
        ("Grand Chess", "Pawnstar Games", "demo.grand.chess", 402_100_000, true),
        ("Harbor Maps", "Compass & Co", "demo.harbor.maps", 318_500_000, false),
        ("Lingo Coach", "Wordsmith", "demo.lingo.coach", 254_000_000, false),
        ("Mindful", "Calm Waters", "demo.mindful.app", 143_800_000, false),
        ("Pocket Budget", "Ledgerly", "demo.pocket.budget", 58_200_000, false),
        ("Run Tracker", "Stride", "demo.run.tracker", 97_600_000, false),
        ("Sky Atlas", "Stargazer", "demo.sky.atlas", 512_900_000, false),
        ("Snapshot Studio", "Lumen", "demo.snapshot.studio", 288_300_000, false),
        ("Voice Notes", "Quill", "demo.voice.notes", 41_700_000, false),
    ];

    private readonly DateTime _started = DateTime.UtcNow;
    private readonly Timer _timer;
    private IReadOnlyList<ConnectedDevice> _devices = [];
    private bool _targetTrusted;
    private IpatoolAccount? _account;

    public DemoBackend()
    {
        _timer = new Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public event EventHandler? Changed;

    public bool IsAvailable => true;

    public IReadOnlyList<ConnectedDevice> Devices => _devices;

    public bool? ServiceAvailable => true;

    public void Start() => _timer.Change(500, 500);

    public void Dispose() => _timer.Dispose();

    // Devices ----------------------------------------------------------------------------

    public async Task<DeviceInfo> GetInfoAsync(string udid, CancellationToken cancellationToken = default)
    {
        await Task.Delay(500, cancellationToken);
        return udid == SourceUdid
            ? new DeviceInfo(udid, "Anna’s iPhone", "iPhone12,1", "iPhone 11", "18.6", DeviceTrust.Trusted, 128_000_000_000, 21_400_000_000)
            : new DeviceInfo(udid, "iPhone 17 Pro", "iPhone18,1", "iPhone 17 Pro", "26.1",
                _targetTrusted ? DeviceTrust.Trusted : DeviceTrust.NotTrusted, 256_000_000_000, _targetTrusted ? 231_800_000_000 : null);
    }

    public async Task<bool> TrustAsync(string udid, IProgress<DeviceTrust>? progress, CancellationToken cancellationToken = default)
    {
        progress?.Report(DeviceTrust.Locked);
        await Task.Delay(1500, cancellationToken);
        progress?.Report(DeviceTrust.NotTrusted);
        await Task.Delay(2500, cancellationToken);
        _targetTrusted = true;
        return true;
    }

    public async Task<IReadOnlyList<InstalledApp>> GetUserAppsAsync(string udid, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1200, cancellationToken);
        var apps = Catalog.Select((a, i) => new InstalledApp(
            a.Bundle, a.Name, $"{2 + i % 5}.{i * 3 % 10}.{i % 4}", a.Developer, 1_400_000_000 + i, $"8{i:000}000", "anna@example.com", "16.0", a.Size)).ToList();
        apps.Add(new InstalledApp("demo.testflight.beta", "Beta Build", "0.9", null, null, null, null, "17.0", 30_000_000));
        return apps;
    }

    public async Task LoadIconsAsync(string udid, IReadOnlyList<string> bundleIds, Action<string, byte[]> onIcon, CancellationToken cancellationToken = default)
    {
        foreach (var id in bundleIds)
        {
            await Task.Delay(40, cancellationToken);
            var index = Array.FindIndex(Catalog, a => a.Bundle == id);
            onIcon(id, DemoIcons.Create(Math.Max(0, index)));
        }
    }

    public async Task InstallAsync(string udid, string ipaPath, IProgress<InstallProgress>? progress, CancellationToken cancellationToken = default)
    {
        for (var i = 1; i <= 20; i++)
        {
            await Task.Delay(45, cancellationToken);
            progress?.Report(new InstallProgress(InstallPhase.Uploading, i / 20.0));
        }

        foreach (var percent in new[] { 5, 15, 30, 50, 70, 90, 100 })
        {
            await Task.Delay(130, cancellationToken);
            progress?.Report(new InstallProgress(InstallPhase.Installing, percent / 100.0));
        }
    }

    // App Store --------------------------------------------------------------------------

    public Task<IpatoolAccount?> GetAccountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_account);

    public async Task<LoginResult> LoginAsync(string email, string password, string? authCode, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1200, cancellationToken);
        if (string.IsNullOrWhiteSpace(authCode))
            return new LoginResult(LoginStatus.CodeRequired);
        _account = new IpatoolAccount(email, "Anna Example");
        return new LoginResult(LoginStatus.SignedIn, _account);
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        _account = null;
        return Task.CompletedTask;
    }

    public async Task<string> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default)
    {
        var app = Catalog.FirstOrDefault(a => a.Bundle == request.BundleId);
        await Task.Delay(400, cancellationToken);
        if (app.Paid)
            throw new IpatoolException(IpatoolErrorKind.PaidAppNotSupported, "purchasing paid apps is not supported");

        var total = Math.Max(app.Size, 20_000_000) / 3;
        for (var i = 1; i <= 25; i++)
        {
            await Task.Delay(70, cancellationToken);
            progress?.Report(new DownloadProgress(DownloadPhase.Downloading, i / 25.0, total * i / 25));
        }

        progress?.Report(new DownloadProgress(DownloadPhase.Finalizing, 1, total));
        await Task.Delay(300, cancellationToken);
        WriteFakeIpa(request.OutputPath, request.BundleId);
        return request.OutputPath;
    }

    private static void WriteFakeIpa(string path, string bundleId)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("Payload/Demo.app/Info.plist");
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write($"""
            <?xml version="1.0" encoding="UTF-8"?>
            <plist version="1.0"><dict>
            <key>CFBundleIdentifier</key><string>{bundleId}</string>
            <key>CFBundleShortVersionString</key><string>1.0</string>
            </dict></plist>
            """);
    }

    private void Tick()
    {
        var elapsed = DateTime.UtcNow - _started;
        List<ConnectedDevice> devices = [];
        if (elapsed > TimeSpan.FromSeconds(1))
            devices.Add(new ConnectedDevice(SourceUdid, 1));
        if (elapsed > TimeSpan.FromSeconds(4))
            devices.Add(new ConnectedDevice(TargetUdid, 2));
        if (devices.Count == _devices.Count)
            return;
        _devices = devices;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
