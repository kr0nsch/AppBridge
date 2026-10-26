using CommunityToolkit.Mvvm.ComponentModel;
using AppBridge.Core.Devices;
using AppBridge.Core.Library;
using AppBridge.Services;
using Microsoft.UI.Xaml.Media;

namespace AppBridge.ViewModels;

public sealed partial class AppItemViewModel(InstalledApp app, SavedApp? saved = null) : ObservableObject
{
    public InstalledApp App { get; } = app;

    /// <summary>The file to install from, for apps saved on this PC.</summary>
    public SavedApp? Saved { get; } = saved;

    public string Name => App.Name;

    public string Detail
    {
        get
        {
            var version = Loc.Format("App_Version", App.Version);
            var detail = string.IsNullOrEmpty(App.Developer) ? version : $"{App.Developer}  ·  {version}";
            return string.IsNullOrEmpty(Account) ? detail : $"{detail}  ·  {Account}";
        }
    }

    public string SizeText => App.SizeBytes is { } size ? Format.Bytes(size) : "";

    public string Initial => string.IsNullOrEmpty(App.Name) ? "?" : App.Name[..1].ToUpperInvariant();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon), nameof(ShowInitial))]
    public partial ImageSource? Icon { get; set; }

    public bool HasIcon => Icon is not null;

    public bool ShowInitial => Icon is null;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>False for apps this iPhone can't run: dimmed, without a checkmark.</summary>
    public bool IsSelectable { get; init; } = true;

    /// <summary>A short status line, such as "Already on iPhone".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCaption))]
    public partial string? Caption { get; set; }

    public bool HasCaption => !string.IsNullOrEmpty(Caption);

    /// <summary>Blue for good news ("Updates 1.0 → 1.1"), gray otherwise.</summary>
    public bool CaptionIsAccent { get; init; }

    /// <summary>Apple Account shown on each row when saved apps come from more than one account.</summary>
    public string? Account { get; init; }

    /// <summary>Header drawn above the first row of the "can't install" group.</summary>
    public string SectionHeader { get; init; } = "";

    [ObservableProperty]
    public partial bool ShowSectionHeader { get; set; }

    /// <summary>Hidden for the first row of a group, as in iOS inset lists.</summary>
    [ObservableProperty]
    public partial bool ShowSeparator { get; set; }

    public override string ToString() => HasCaption ? $"{Name}, {Detail}, {Caption}" : $"{Name}, {Detail}";

    public bool Matches(string query) =>
        string.IsNullOrWhiteSpace(query)
        || App.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || (App.Developer?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false)
        || App.BundleId.Contains(query, StringComparison.OrdinalIgnoreCase);
}
