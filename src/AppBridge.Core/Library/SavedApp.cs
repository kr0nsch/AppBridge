using AppBridge.Core.Devices;

namespace AppBridge.Core.Library;

/// <summary>An app kept on this PC as an .ipa file, ready to install without the App Store.</summary>
public sealed record SavedApp(
    string FilePath,
    string BundleId,
    string Name,
    string Version,
    string? Developer,
    long? AppStoreId,
    string? ExternalVersionId,
    string? PurchaserAppleId,
    string? MinimumOsVersion,
    bool RunsOnIPhone,
    long SizeBytes,
    DateTime SavedAtUtc)
{
    /// <summary>The same app in the shape the transfer pipeline and the lists use.</summary>
    public InstalledApp ToInstalledApp() =>
        new(BundleId, Name, Version, Developer, AppStoreId, ExternalVersionId, PurchaserAppleId, MinimumOsVersion, SizeBytes);
}

/// <summary>What a scan of the saved-apps folder found.</summary>
/// <param name="Apps">One entry per app: its newest saved version.</param>
/// <param name="Versions">Every usable file, newest version first within each app.</param>
/// <param name="UnreadableFiles">.ipa files that are damaged, not iPhone apps, or carry no App Store license.</param>
public sealed record LibraryScan(IReadOnlyList<SavedApp> Apps, IReadOnlyList<SavedApp> Versions, IReadOnlyList<string> UnreadableFiles)
{
    public static LibraryScan Empty { get; } = new([], [], []);

    /// <summary>Files not listed because a newer copy of the same app exists.</summary>
    public int OlderCopies => Versions.Count - Apps.Count;

    public long TotalBytes => Apps.Sum(a => a.SizeBytes);

    /// <summary>Saved versions of one app, newest first.</summary>
    public IEnumerable<SavedApp> VersionsOf(string bundleId) => Versions.Where(v => v.BundleId == bundleId);
}
