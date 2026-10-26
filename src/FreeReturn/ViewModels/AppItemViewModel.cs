using CommunityToolkit.Mvvm.ComponentModel;
using FreeReturn.Core.Devices;
using FreeReturn.Services;
using Microsoft.UI.Xaml.Media;

namespace FreeReturn.ViewModels;

public sealed partial class AppItemViewModel(InstalledApp app) : ObservableObject
{
    public InstalledApp App { get; } = app;

    public string Name => App.Name;

    public string Detail => string.IsNullOrEmpty(App.Developer)
        ? Loc.Format("App_Version", App.Version)
        : $"{App.Developer}  ·  {Loc.Format("App_Version", App.Version)}";

    public string SizeText => App.SizeBytes is { } size ? Format.Bytes(size) : "";

    public string Initial => string.IsNullOrEmpty(App.Name) ? "?" : App.Name[..1].ToUpperInvariant();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon), nameof(ShowInitial))]
    public partial ImageSource? Icon { get; set; }

    public bool HasIcon => Icon is not null;

    public bool ShowInitial => Icon is null;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Hidden for the first row of a group, as in iOS inset lists.</summary>
    [ObservableProperty]
    public partial bool ShowSeparator { get; set; }

    public override string ToString() => $"{Name}, {Detail}";

    public bool Matches(string query) =>
        string.IsNullOrWhiteSpace(query)
        || App.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || (App.Developer?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false)
        || App.BundleId.Contains(query, StringComparison.OrdinalIgnoreCase);
}
