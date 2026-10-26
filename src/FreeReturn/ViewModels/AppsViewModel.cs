using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeReturn.Core;
using FreeReturn.Core.Devices;
using FreeReturn.Services;
using Microsoft.UI.Dispatching;

namespace FreeReturn.ViewModels;

/// <summary>Step 2: the App Store apps found on the source iPhone and which ones to copy.</summary>
public sealed partial class AppsViewModel : ObservableObject
{
    private readonly IDeviceService _devices;
    private readonly IconCache _icons;
    private readonly DispatcherQueue _dispatcher;
    private List<AppItemViewModel> _all = [];
    private CancellationTokenSource? _loading;

    public AppsViewModel(IDeviceService devices, IconCache icons, DispatcherQueue dispatcher)
    {
        _devices = devices;
        _icons = icons;
        _dispatcher = dispatcher;
    }

    public ObservableCollection<AppItemViewModel> Visible { get; } = [];

    public string? LoadedFromUdid { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowList), nameof(ShowEmpty))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(ShowList), nameof(ShowEmpty))]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionSummary), nameof(HasSelection), nameof(SelectAllText))]
    public partial int SelectedCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotFromStoreText), nameof(HasNotFromStore))]
    public partial int NotFromStoreCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowList), nameof(ShowEmpty))]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoMatches))]
    public partial int VisibleCount { get; set; }

    public bool HasError => !string.IsNullOrEmpty(Error);

    public bool ShowList => !IsLoading && !HasError && TotalCount > 0;

    public bool ShowEmpty => !IsLoading && !HasError && TotalCount == 0;

    public bool ShowNoMatches => ShowList && VisibleCount == 0;

    public bool HasSelection => SelectedCount > 0;

    public bool HasNotFromStore => NotFromStoreCount > 0;

    public string NotFromStoreText => Loc.Plural("Apps_NotFromStore", NotFromStoreCount);

    public string SelectAllText => _all.Count > 0 && SelectedCount == _all.Count ? Loc.Get("Apps_DeselectAll") : Loc.Get("Apps_SelectAll");

    public string SelectionSummary
    {
        get
        {
            if (SelectedCount == 0)
                return Loc.Get("Apps_NoneSelected");
            var bytes = _all.Where(a => a.IsSelected).Sum(a => a.App.SizeBytes ?? 0);
            var count = Loc.Plural("Apps_SelectedCount", SelectedCount);
            return bytes > 0 ? $"{count}  ·  {Format.Bytes(bytes)}" : count;
        }
    }

    public IReadOnlyList<InstalledApp> SelectedApps => _all.Where(a => a.IsSelected).Select(a => a.App).ToList();

    public async Task LoadAsync(DeviceItemViewModel source)
    {
        if (LoadedFromUdid == source.Udid && Error is null)
            return;

        _loading?.Cancel();
        _loading = new CancellationTokenSource();
        var token = _loading.Token;

        IsLoading = true;
        Error = null;
        Clear();
        try
        {
            var apps = await _devices.GetUserAppsAsync(source.Udid, token);
            NotFromStoreCount = apps.Count(a => !a.IsFromAppStore);
            _all = apps.Where(a => a.IsFromAppStore).Select(a => new AppItemViewModel(a)).ToList();
            foreach (var item in _all)
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(AppItemViewModel.IsSelected))
                        SelectedCount = _all.Count(a => a.IsSelected);
                };
            TotalCount = _all.Count;
            LoadedFromUdid = source.Udid;
            ApplyFilter();
            _ = LoadIconsAsync(source.Udid, token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (DeviceOperationException ex)
        {
            AppLog.Warn($"Could not read apps: {ex.Kind} {ex.Message}");
            Error = ex.Kind switch
            {
                DeviceErrorKind.Disconnected or DeviceErrorKind.ServiceUnavailable => Loc.Get("Apps_Error_Disconnected"),
                DeviceErrorKind.Locked => Loc.Get("Device_Status_Unlock"),
                DeviceErrorKind.NotTrusted => Loc.Get("Device_Status_TapTrust"),
                _ => Loc.Format("Apps_Error_Generic", ex.Message),
            };
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsLoading = false;
        }
    }

    public void Reset()
    {
        _loading?.Cancel();
        LoadedFromUdid = null;
        Error = null;
        IsLoading = false;
        Clear();
    }

    public void ClearSelection()
    {
        foreach (var item in _all)
            item.IsSelected = false;
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        var select = SelectedCount != _all.Count;
        foreach (var item in _all)
            item.IsSelected = select;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void Clear()
    {
        _all = [];
        Visible.Clear();
        TotalCount = 0;
        VisibleCount = 0;
        SelectedCount = 0;
        NotFromStoreCount = 0;
    }

    private void ApplyFilter()
    {
        var matches = _all.Where(a => a.Matches(SearchText.Trim())).ToList();
        Visible.Clear();
        for (var i = 0; i < matches.Count; i++)
        {
            matches[i].ShowSeparator = i > 0;
            Visible.Add(matches[i]);
        }

        VisibleCount = matches.Count;
    }

    private async Task LoadIconsAsync(string udid, CancellationToken token)
    {
        try
        {
            var ids = _all.Select(a => a.App.BundleId).ToList();
            await _devices.LoadIconsAsync(udid, ids, (bundleId, png) =>
            {
                _icons.Store(bundleId, png);
                _dispatcher.TryEnqueue(async () =>
                {
                    var item = _all.FirstOrDefault(a => a.App.BundleId == bundleId);
                    if (item is not null)
                        item.Icon = await _icons.GetAsync(bundleId);
                });
            }, token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or DeviceOperationException)
        {
            AppLog.Debug($"Icon loading stopped: {ex.Message}");
        }
    }
}
