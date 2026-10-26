using System.IO.Compression;
using System.Text;
using AppBridge.Core;
using AppBridge.Core.Devices;
using AppBridge.Core.Ipatool;
using AppBridge.Core.Library;

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
        if (udid == TargetUdid)
        {
            // The new iPhone already has two of the apps: one up to date, one older.
            return
            [
                new InstalledApp(Catalog[1].Bundle, Catalog[1].Name, VersionOf(1), Catalog[1].Developer, 1_400_000_001, null, "anna@example.com", "16.0", Catalog[1].Size),
                new InstalledApp(Catalog[8].Bundle, Catalog[8].Name, "1.0.0", Catalog[8].Developer, 1_400_000_008, null, "anna@example.com", "16.0", Catalog[8].Size),
            ];
        }

        var apps = Catalog.Select((a, i) => new InstalledApp(
            a.Bundle, a.Name, VersionOf(i), a.Developer, 1_400_000_000 + i, $"8{i:000}000", "anna@example.com", "16.0", a.Size)).ToList();
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

    /// <summary>
    /// Fills the demo folder with a few saved apps, as if "Transfer Later" had been used before.
    /// Each demo launch starts from a clean folder so screenshots are repeatable.
    /// </summary>
    public static void PrepareSavedApps(string folder, bool withSavedApps)
    {
        try
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
            Directory.CreateDirectory(folder);
            if (!withSavedApps)
                return;
            foreach (var index in new[] { 0, 1, 3, 4, 6, 7, 8, 9, 10, 11, 12 })
                WriteFakeIpa(Path.Combine(folder, $"{Catalog[index].Name} {VersionOf(index)}.ipa"), Catalog[index].Bundle);
        }
        catch (IOException)
        {
            // Another demo instance is using the folder; the list just stays as it is.
        }
    }

    private static string VersionOf(int index) => $"{2 + index % 5}.{index * 3 % 10}.{index % 4}";

    /// <summary>An .ipa with real-looking metadata and icon, so saved demo apps list like real ones.</summary>
    private static void WriteFakeIpa(string path, string bundleId)
    {
        var index = Math.Max(0, Array.FindIndex(Catalog, a => a.Bundle == bundleId));
        var app = Catalog[index];
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteText(zip, "Payload/Demo.app/Info.plist", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <plist version="1.0"><dict>
            <key>CFBundleIdentifier</key><string>{bundleId}</string>
            <key>CFBundleDisplayName</key><string>{app.Name}</string>
            <key>CFBundleShortVersionString</key><string>{VersionOf(index)}</string>
            <key>MinimumOSVersion</key><string>{(index == 11 ? "27.0" : "16.0")}</string>
            </dict></plist>
            """);
        WriteText(zip, "iTunesMetadata.plist", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <plist version="1.0"><dict>
            <key>itemName</key><string>{app.Name}</string>
            <key>artistName</key><string>{app.Developer.Replace("&", "&amp;")}</string>
            <key>itemId</key><integer>{1_400_000_000 + index}</integer>
            <key>apple-id</key><string>anna@example.com</string>
            </dict></plist>
            """);
        WriteText(zip, "Payload/Demo.app/SC_Info/Demo.sinf", "demo");
        using var icon = zip.CreateEntry("Payload/Demo.app/AppIcon60x60@2x.png").Open();
        icon.Write(DemoIcons.Create(index));
    }

    /// <summary>Demo files are tiny; show the sizes the fictional apps would really have.</summary>
    public static LibraryScan WithRealisticSizes(LibraryScan scan)
    {
        static SavedApp Resize(SavedApp a) => Array.FindIndex(Catalog, c => c.Bundle == a.BundleId) is var i and >= 0
            ? a with { SizeBytes = Catalog[i].Size * 7 / 10 }
            : a;
        return scan with { Apps = scan.Apps.Select(Resize).ToList(), Versions = scan.Versions.Select(Resize).ToList() };
    }

    private static void WriteText(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(text);
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
