using AppBridge.Core;
using AppBridge.Core.Devices;
using AppBridge.Core.Library;
using AppBridge.Services;

namespace AppBridge.ViewModels;

/// <summary>
/// "Transfer Later", step 2: the apps saved on this PC, checked against the new iPhone. Each app
/// uses its newest saved version that this iPhone can run; apps it can't run are listed last.
/// </summary>
public sealed partial class SavedAppsViewModel(IDeviceService devices, IconCache icons) : AppListViewModel
{
    private CancellationTokenSource? _loading;
    private long? _freeBytes;
    private string _folderName = "";

    public override string LoadingText => Loc.Get("Saved_Loading");

    public override string EmptyText => Loc.Format("Saved_Empty", _folderName);

    public bool HasCompatible => All.Any(a => a.IsSelectable);

    public IReadOnlyList<SavedApp> SelectedApps => All.Where(a => a.IsSelected && a.Saved is not null).Select(a => a.Saved!).ToList();

    public override string SelectionSummary
    {
        get
        {
            if (TotalCount > 0 && !HasCompatible)
                return Loc.Get("Saved_NothingCompatible");
            if (SelectedCount > 0 && _freeBytes is { } free && SelectedBytes > free)
                return Loc.Format("Saved_NotEnoughSpace", Format.Bytes(SelectedBytes), Format.Bytes(free));
            return base.SelectionSummary;
        }
    }

    public async Task LoadAsync(DeviceItemViewModel target, LibraryScan library, string folder)
    {
        _loading?.Cancel();
        _loading = new CancellationTokenSource();
        var token = _loading.Token;

        IsLoading = true;
        Error = null;
        Clear();
        _freeBytes = target.FreeBytes;
        _folderName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } name ? name : folder;
        try
        {
            IReadOnlyList<InstalledApp>? installed = null;
            try
            {
                installed = await devices.GetUserAppsAsync(target.Udid, token);
            }
            catch (DeviceOperationException ex)
            {
                // Without the list the apps are still installable; only "Already on iPhone" is missing.
                AppLog.Warn($"Could not read the apps on the new iPhone: {ex.Kind} {ex.Message}");
            }

            token.ThrowIfCancellationRequested();
            var items = Build(library, target.OsVersion, installed);
            SetItems(items);
            UpdateFootnote(library, items);
            OnPropertyChanged(nameof(HasCompatible));
            OnPropertyChanged(nameof(SelectionSummary));
            _ = LoadIconsAsync(items, token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsLoading = false;
        }
    }

    /// <summary>Shows the spinner right away, before the folder scan that precedes <see cref="LoadAsync"/>.</summary>
    public void ShowLoading()
    {
        _loading?.Cancel();
        Error = null;
        Clear();
        IsLoading = true;
    }

    public void Reset()
    {
        _loading?.Cancel();
        IsLoading = false;
        Error = null;
        Clear();
    }

    private static List<AppItemViewModel> Build(LibraryScan library, string osVersion, IReadOnlyList<InstalledApp>? installed)
    {
        var accounts = library.Apps.Select(a => a.PurchaserAppleId).Where(a => !string.IsNullOrEmpty(a)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        List<AppItemViewModel> compatible = [];
        List<AppItemViewModel> incompatible = [];
        foreach (var newest in library.Apps)
        {
            var account = accounts > 1 ? newest.PurchaserAppleId : null;
            var chosen = library.VersionsOf(newest.BundleId).FirstOrDefault(v => v.RunsOnIPhone && RunsOn(v.MinimumOsVersion, osVersion));
            if (chosen is null)
            {
                incompatible.Add(new AppItemViewModel(newest.ToInstalledApp(), newest)
                {
                    IsSelectable = false,
                    Caption = newest.RunsOnIPhone ? Loc.Format("Saved_NeedsIos", newest.MinimumOsVersion) : Loc.Get("Saved_IPadOnly"),
                    SectionHeader = Loc.Get("Saved_IncompatibleHeader"),
                    Account = account,
                });
                continue;
            }

            List<string> notes = [];
            if (chosen.FilePath != newest.FilePath)
                notes.Add(Loc.Format("Saved_OlderVersion", chosen.Version, newest.Version, newest.MinimumOsVersion));

            var onPhone = installed?.FirstOrDefault(i => i.BundleId == chosen.BundleId);
            var select = true;
            var accent = false;
            if (onPhone is not null)
            {
                if (SavedAppLibrary.CompareVersions(onPhone.Version, chosen.Version) >= 0)
                {
                    notes.Add(Loc.Get("Saved_AlreadyInstalled"));
                    select = false;
                }
                else
                {
                    notes.Add(Loc.Format("Saved_Updates", onPhone.Version, chosen.Version));
                    accent = notes.Count == 1;
                }
            }

            compatible.Add(new AppItemViewModel(chosen.ToInstalledApp(), chosen)
            {
                IsSelected = select,
                Caption = notes.Count == 0 ? null : string.Join("  ·  ", notes),
                CaptionIsAccent = accent,
                Account = account,
            });
        }

        return [.. compatible, .. incompatible];
    }

    private static bool RunsOn(string? minimumOs, string osVersion) =>
        string.IsNullOrEmpty(minimumOs) || string.IsNullOrEmpty(osVersion) || SavedAppLibrary.CompareVersions(minimumOs, osVersion) <= 0;

    private void UpdateFootnote(LibraryScan library, List<AppItemViewModel> items)
    {
        List<string> lines = [];
        if (items.Any(i => !i.IsSelectable && i.Saved?.RunsOnIPhone == true))
            lines.Add(Loc.Get("Saved_UpdateIos"));
        if (library.UnreadableFiles.Count > 0)
        {
            lines.Add(Loc.Format("Saved_Unreadable", library.UnreadableFiles.Count));
            FootnoteDetail = string.Join(Environment.NewLine, library.UnreadableFiles.Select(Path.GetFileName));
        }

        Footnote = string.Join(Environment.NewLine, lines);
    }

    /// <summary>Runs on the UI thread; reading icons out of the files happens in the background.</summary>
    private async Task LoadIconsAsync(List<AppItemViewModel> items, CancellationToken token)
    {
        foreach (var item in items)
        {
            if (token.IsCancellationRequested)
                return;
            item.Icon = await icons.GetAsync(item.Saved!);
        }
    }
}
