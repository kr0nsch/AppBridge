using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppBridge.Services;

namespace AppBridge.ViewModels;

/// <summary>
/// A searchable, checkable list of apps. The apps step shows either the apps on the source
/// iPhone (<see cref="AppsViewModel"/>) or the apps saved on this PC (<see cref="SavedAppsViewModel"/>).
/// </summary>
public abstract partial class AppListViewModel : ObservableObject
{
    private List<AppItemViewModel> _all = [];

    public ObservableCollection<AppItemViewModel> Visible { get; } = [];

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
    [NotifyPropertyChangedFor(nameof(ShowList), nameof(ShowEmpty))]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoMatches))]
    public partial int VisibleCount { get; set; }

    /// <summary>Small print under the list, such as apps that can't be shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFootnote))]
    public partial string Footnote { get; set; } = "";

    [ObservableProperty]
    public partial string? FootnoteDetail { get; set; }

    public bool HasFootnote => Footnote.Length > 0;

    public bool HasError => !string.IsNullOrEmpty(Error);

    public bool ShowList => !IsLoading && !HasError && TotalCount > 0;

    public bool ShowEmpty => !IsLoading && !HasError && TotalCount == 0;

    public bool ShowNoMatches => ShowList && VisibleCount == 0;

    public bool HasSelection => SelectedCount > 0;

    public abstract string LoadingText { get; }

    public abstract string EmptyText { get; }

    public string SelectAllText => SelectionScope.Count > 0 && SelectionScope.All(a => a.IsSelected) ? Loc.Get("Apps_DeselectAll") : Loc.Get("Apps_SelectAll");

    public virtual string SelectionSummary
    {
        get
        {
            if (SelectedCount == 0)
                return Loc.Get("Apps_NoneSelected");
            var bytes = SelectedBytes;
            var count = Loc.Plural("Apps_SelectedCount", SelectedCount);
            return bytes > 0 ? $"{count}  ·  {Format.Bytes(bytes)}" : count;
        }
    }

    protected IReadOnlyList<AppItemViewModel> All => _all;

    protected long SelectedBytes => _all.Where(a => a.IsSelected).Sum(a => a.App.SizeBytes ?? 0);

    /// <summary>While searching, Select All applies to the matching apps only; apps that can't be installed never count.</summary>
    private List<AppItemViewModel> SelectionScope => (string.IsNullOrWhiteSpace(SearchText) ? _all : Visible.AsEnumerable()).Where(a => a.IsSelectable).ToList();

    public void ClearSelection()
    {
        foreach (var item in _all)
            item.IsSelected = false;
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        var scope = SelectionScope;
        var select = scope.Any(a => !a.IsSelected);
        foreach (var item in scope)
            item.IsSelected = select;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(SelectAllText));
    }

    protected void SetItems(List<AppItemViewModel> items)
    {
        _all = items;
        foreach (var item in items)
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppItemViewModel.IsSelected))
                    SelectedCount = _all.Count(a => a.IsSelected);
            };
        TotalCount = items.Count;
        SelectedCount = items.Count(a => a.IsSelected);
        ApplyFilter();
    }

    protected void Clear()
    {
        _all = [];
        Visible.Clear();
        TotalCount = 0;
        VisibleCount = 0;
        SelectedCount = 0;
        Footnote = "";
        FootnoteDetail = null;
    }

    private void ApplyFilter()
    {
        var matches = _all.Where(a => a.Matches(SearchText.Trim())).ToList();
        Visible.Clear();
        var headerShown = false;
        for (var i = 0; i < matches.Count; i++)
        {
            matches[i].ShowSeparator = i > 0;
            matches[i].ShowSectionHeader = !matches[i].IsSelectable && !headerShown && matches[i].SectionHeader.Length > 0;
            headerShown |= matches[i].ShowSectionHeader;
            Visible.Add(matches[i]);
        }

        VisibleCount = matches.Count;
    }
}
