using FreeReturn.Core.Devices;
using FreeReturn.Core.Ipatool;

namespace FreeReturn.Core;

/// <summary>App Store access: sign-in and downloading licensed app packages.</summary>
public interface IAppStoreClient
{
    bool IsAvailable { get; }

    Task<IpatoolAccount?> GetAccountAsync(CancellationToken cancellationToken = default);

    Task<LoginResult> LoginAsync(string email, string password, string? authCode, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);

    Task<string> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default);
}

/// <summary>Operations on one connected iPhone, addressed by UDID.</summary>
public interface IDeviceService
{
    Task<DeviceInfo> GetInfoAsync(string udid, CancellationToken cancellationToken = default);

    Task<bool> TrustAsync(string udid, IProgress<DeviceTrust>? progress, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InstalledApp>> GetUserAppsAsync(string udid, CancellationToken cancellationToken = default);

    Task LoadIconsAsync(string udid, IReadOnlyList<string> bundleIds, Action<string, byte[]> onIcon, CancellationToken cancellationToken = default);

    Task InstallAsync(string udid, string ipaPath, IProgress<InstallProgress>? progress, CancellationToken cancellationToken = default);
}

/// <summary>Keeps track of which iPhones are plugged in.</summary>
public interface IDeviceWatcher : IDisposable
{
    event EventHandler? Changed;

    IReadOnlyList<ConnectedDevice> Devices { get; }

    bool? ServiceAvailable { get; }

    void Start();
}
