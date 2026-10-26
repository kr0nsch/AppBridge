using System.Reflection;
using AppBridge.Core;
using AppBridge.Core.Devices;
using AppBridge.Core.Ipatool;
using AppBridge.Core.Transfer;
using AppBridge.Demo;

namespace AppBridge.Services;

/// <summary>Long-lived services shared by the whole app.</summary>
public sealed class AppServices : IDisposable
{
    private readonly string? _demoDownloads;

    private AppServices(AppPaths paths, bool demo)
    {
        Paths = paths;
        IsDemo = demo;
        Settings = new SettingsStore(paths.SettingsFile);

        if (demo)
        {
            // Simulated iPhones and App Store for UI work and screenshots; touches nothing real.
            var backend = new DemoBackend();
            AppStore = backend;
            Devices = backend;
            Watcher = backend;
            _demoDownloads = Path.Combine(Path.GetTempPath(), "AppBridgeDemo");
            DemoBackend.PrepareSavedApps(_demoDownloads, HasFlag("--demo-saved") || Environment.GetEnvironmentVariable("APPBRIDGE_DEMO_SAVED") == "1");
        }
        else
        {
            AppStore = new IpatoolClient(paths.IpatoolExecutable, paths.IpatoolHome);
            Devices = new DeviceService(paths.PairRecordsDirectory);
            Watcher = new DeviceWatcher();
        }

        Transfers = new TransferRunner(AppStore, Devices);
    }

    public static string Version { get; } =
        typeof(AppServices).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    public const string RepositoryUrl = "https://github.com/kr0nsch/AppBridge";

    public AppPaths Paths { get; }

    public bool IsDemo { get; }

    public SettingsStore Settings { get; }

    public IAppStoreClient AppStore { get; }

    public IDeviceService Devices { get; }

    public IDeviceWatcher Watcher { get; }

    public TransferRunner Transfers { get; }

    /// <summary>Icons survive restarts so saved apps keep them; demo icons stay out of the real cache.</summary>
    public string IconsDirectory => _demoDownloads is null ? Paths.IconsDirectory : Path.Combine(Path.GetTempPath(), "AppBridgeDemoIcons");

    public string DownloadDirectory =>
        _demoDownloads
        ?? (string.IsNullOrWhiteSpace(Settings.Current.DownloadDirectory) ? Paths.DefaultDownloadDirectory : Settings.Current.DownloadDirectory);

    public static AppServices Create()
    {
        var paths = AppPaths.Detect();
        paths.EnsureCreated();
        AppLog.Initialize(paths.LogsDirectory);
        var demo = HasFlag("--demo") || HasFlag("--demo-saved") || Environment.GetEnvironmentVariable("APPBRIDGE_DEMO") == "1";
        return new AppServices(paths, demo);
    }

    public void Dispose() => Watcher.Dispose();

    private static bool HasFlag(string flag) => Environment.GetCommandLineArgs().Any(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));
}
