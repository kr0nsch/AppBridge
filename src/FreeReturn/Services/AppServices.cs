using System.Reflection;
using FreeReturn.Core;
using FreeReturn.Core.Devices;
using FreeReturn.Core.Ipatool;
using FreeReturn.Core.Transfer;
using FreeReturn.Demo;

namespace FreeReturn.Services;

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
            _demoDownloads = Path.Combine(Path.GetTempPath(), "FreeReturnDemo");
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

    public const string RepositoryUrl = "https://github.com/kr0nsch/FreeReturn";

    public AppPaths Paths { get; }

    public bool IsDemo { get; }

    public SettingsStore Settings { get; }

    public IAppStoreClient AppStore { get; }

    public IDeviceService Devices { get; }

    public IDeviceWatcher Watcher { get; }

    public TransferRunner Transfers { get; }

    public string DownloadDirectory =>
        _demoDownloads
        ?? (string.IsNullOrWhiteSpace(Settings.Current.DownloadDirectory) ? Paths.DefaultDownloadDirectory : Settings.Current.DownloadDirectory);

    public static AppServices Create()
    {
        var paths = AppPaths.Detect();
        paths.EnsureCreated();
        AppLog.Initialize(paths.LogsDirectory);
        var demo = Environment.GetCommandLineArgs().Any(a => a.Equals("--demo", StringComparison.OrdinalIgnoreCase))
                   || Environment.GetEnvironmentVariable("FREERETURN_DEMO") == "1";
        return new AppServices(paths, demo);
    }

    public void Dispose() => Watcher.Dispose();
}
