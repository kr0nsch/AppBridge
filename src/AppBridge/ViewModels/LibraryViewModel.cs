using CommunityToolkit.Mvvm.ComponentModel;
using AppBridge.Core;
using AppBridge.Core.Library;
using AppBridge.Demo;
using AppBridge.Services;

namespace AppBridge.ViewModels;

/// <summary>The apps saved on this PC for "Transfer Later", rescanned whenever a screen needs them.</summary>
public sealed partial class LibraryViewModel(AppServices services) : ObservableObject
{
    private CancellationTokenSource? _scan;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Count), nameof(HasApps), nameof(TotalBytes))]
    public partial LibraryScan Scan { get; private set; } = LibraryScan.Empty;

    [ObservableProperty]
    public partial bool FolderExists { get; private set; }

    public string Folder => services.DownloadDirectory;

    public int Count => Scan.Apps.Count;

    public bool HasApps => Count > 0;

    public long TotalBytes => Scan.TotalBytes;

    /// <summary>"Downloads › AppBridge" for the default folder, the full path otherwise.</summary>
    public string FolderDisplay
    {
        get
        {
            var folder = Folder.TrimEnd(Path.DirectorySeparatorChar);
            var defaultFolder = services.Paths.DefaultDownloadDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return services.IsDemo || string.Equals(folder, defaultFolder, StringComparison.OrdinalIgnoreCase)
                ? $"{Loc.Get("Folder_Downloads")} › {Path.GetFileName(services.IsDemo ? defaultFolder : folder)}"
                : folder;
        }
    }

    public string FolderName => services.IsDemo ? "AppBridge" : Path.GetFileName(Folder.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } name ? name : Folder;

    public async Task RefreshAsync()
    {
        _scan?.Cancel();
        _scan = new CancellationTokenSource();
        var token = _scan.Token;
        var folder = Folder;
        var language = Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride is { Length: > 0 } lang
            ? lang
            : System.Globalization.CultureInfo.CurrentUICulture.Name;
        try
        {
            // Directory.Exists can stall on an offline network folder, so it runs in the background too.
            var (scan, exists) = await Task.Run(() => (SavedAppLibrary.Scan(folder, language, token), Directory.Exists(folder)), token);
            if (services.IsDemo)
                scan = DemoBackend.WithRealisticSizes(scan);
            Scan = scan;
            FolderExists = exists;
            OnPropertyChanged(nameof(Folder));
            OnPropertyChanged(nameof(FolderDisplay));
            OnPropertyChanged(nameof(FolderName));
            AppLog.Info($"Saved apps: {scan.Apps.Count} ({scan.UnreadableFiles.Count} unreadable, {scan.OlderCopies} older copies).");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppLog.Warn("Could not read the saved apps folder.", ex);
            Scan = LibraryScan.Empty;
            FolderExists = false;
        }
    }

    public void ChangeFolder(string folder)
    {
        services.Settings.Current.DownloadDirectory = folder;
        services.Settings.Save();
        AppLog.Info("Download folder changed.");
    }

    public void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(Folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("Could not create the saved apps folder.", ex);
        }

        Views.Shell.Open(Folder);
    }
}
