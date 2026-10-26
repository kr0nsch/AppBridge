using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppBridge.Services;
using Microsoft.UI.Dispatching;

namespace AppBridge.ViewModels;

public enum WizardStep
{
    Source,
    Apps,
    Target,
    Install,
}

/// <summary>
/// The whole flow: pick the source iPhone, choose apps, pick the target iPhone, install.
/// Owns the shared footer (Back / Continue) so every step behaves the same way.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel(AppServices services, DispatcherQueue dispatcher)
    {
        Services = services;
        Icons = new IconCache();
        Devices = new DeviceListViewModel(services.Watcher, services.Devices, dispatcher);
        Apps = new AppsViewModel(services.Devices, Icons, dispatcher);
        Account = new AccountViewModel(services.AppStore);
        Install = new InstallViewModel(services, Icons, dispatcher);

        Devices.StateChanged += (_, _) => OnDevicesChanged();
        Apps.PropertyChanged += OnChildChanged;
        Account.PropertyChanged += OnChildChanged;
        Install.PropertyChanged += OnChildChanged;
    }

    public AppServices Services { get; }

    public IconCache Icons { get; }

    public DeviceListViewModel Devices { get; }

    public AppsViewModel Apps { get; }

    public AccountViewModel Account { get; }

    public InstallViewModel Install { get; }

    public bool IsToolMissing => !Services.AppStore.IsAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepIndex), nameof(IsSourceStep), nameof(IsAppsStep), nameof(IsTargetStep), nameof(IsInstallStep), nameof(IsDeviceStep), nameof(DeviceStepTitle), nameof(DeviceStepSubtitle), nameof(DeviceStepTone))]
    public partial WizardStep Step { get; set; }

    [ObservableProperty]
    public partial DeviceItemViewModel? Source { get; set; }

    [ObservableProperty]
    public partial DeviceItemViewModel? Target { get; set; }

    public int StepIndex => (int)Step;

    public bool IsSourceStep => Step == WizardStep.Source;

    public bool IsAppsStep => Step == WizardStep.Apps;

    public bool IsTargetStep => Step == WizardStep.Target;

    public bool IsInstallStep => Step == WizardStep.Install;

    /// <summary>Source and target steps share one device list view.</summary>
    public bool IsDeviceStep => Step is WizardStep.Source or WizardStep.Target;

    public string DeviceStepTitle => Loc.Get(IsTargetStep ? "Target_Title" : "Source_Title");

    public string DeviceStepSubtitle => Loc.Get(IsTargetStep ? "Target_Subtitle" : "Source_Subtitle");

    public string DeviceStepTone => IsTargetStep ? "Teal" : "Blue";

    public string AppsSubtitle => Source is null ? "" : Loc.Format("Apps_Subtitle", Source.Name);

    public string InstallTitle => Install.State switch
    {
        InstallState.Running => Loc.Get("Install_Title_Running"),
        InstallState.Finished when Install.WasCanceled => Loc.Get("Install_Title_Stopped"),
        InstallState.Finished when Install.FailedCount == 0 => Loc.Get("Install_Title_Done"),
        InstallState.Finished => Loc.Get("Install_Title_Problems"),
        _ when !Account.IsSignedIn => Loc.Get("Install_Title_SignIn"),
        _ => Loc.Get("Install_Title_Ready"),
    };

    public string InstallSubtitle => Install.State switch
    {
        InstallState.Running => Loc.Format("Install_Subtitle_Running", Install.CompletedCount + Install.FailedCount, Install.Items.Count, Install.TargetName),
        InstallState.Finished when Install.FailedCount == 0 && !Install.WasCanceled =>
            Loc.Format("Install_Subtitle_Done", Loc.Plural("Apps_Count", Install.CompletedCount), Install.TargetName),
        InstallState.Finished => Loc.Format("Install_Subtitle_Problems", Install.CompletedCount, Install.Items.Count - Install.CompletedCount),
        _ when !Account.IsSignedIn => Loc.Get("Install_Subtitle_SignIn"),
        _ => Loc.Format("Install_Subtitle_Ready", Loc.Plural("Apps_Count", Apps.SelectedCount), Target?.Name ?? "iPhone"),
    };

    public string InstallGlyph => Install.State switch
    {
        InstallState.Finished when Install.FailedCount == 0 && !Install.WasCanceled => "",
        InstallState.Finished => "",
        _ when !Account.IsSignedIn && Install.IsReady => "",
        _ => "",
    };

    public string InstallTone => Install.State switch
    {
        InstallState.Finished when Install.FailedCount == 0 && !Install.WasCanceled => "Green",
        InstallState.Finished => "Orange",
        _ when !Account.IsSignedIn && Install.IsReady => "Indigo",
        _ => "Blue",
    };

    /// <summary>Overall progress for the ring around the hero badge; -1 hides the ring.</summary>
    public double InstallRingProgress => Install.IsRunning ? Install.OverallProgress : -1;

    public bool InstallGlyphIsSuccess => Install.IsFinished && Install.FailedCount == 0 && !Install.WasCanceled;

    public bool InstallGlyphIsWarning => Install.IsFinished && (Install.FailedCount > 0 || Install.WasCanceled);

    public bool InstallGlyphIsNormal => !InstallGlyphIsSuccess && !InstallGlyphIsWarning;

    public bool ShowSignInPanel => IsInstallStep && Install.IsReady && !Account.IsSignedIn;

    public bool ShowInstallSummary => IsInstallStep && Install.IsReady && Account.IsSignedIn;

    public string SummaryFrom => Source?.Name ?? "—";

    public string SummaryTo => Target?.Name ?? "—";

    public string SummaryApps => Loc.Plural("Apps_Count", Apps.SelectedCount);

    // Footer -----------------------------------------------------------------------------

    public bool ShowBack => Step != WizardStep.Source && !(IsInstallStep && !Install.IsReady);

    public string PrimaryText => Step switch
    {
        WizardStep.Install => Install.State switch
        {
            InstallState.Running => Loc.Get("Footer_Stop"),
            InstallState.Finished => Loc.Get("Footer_Done"),
            _ => Loc.Get("Footer_Install"),
        },
        _ => Loc.Get("Footer_Continue"),
    };

    public bool IsPrimaryEnabled => Step switch
    {
        WizardStep.Source => Source?.IsReady == true,
        WizardStep.Apps => Apps.HasSelection && !Apps.IsLoading,
        WizardStep.Target => Target?.IsReady == true && !IsSource(Target),
        WizardStep.Install => Install.State != InstallState.Ready || (Account.IsSignedIn && Target?.IsReady == true),
        _ => false,
    };

    /// <summary>The sign-in form has its own button, so the footer button steps aside.</summary>
    public bool ShowPrimary => !ShowSignInPanel;

    public bool ShowSecondary => IsInstallStep && Install.IsFinished && Install.RetryableCount > 0;

    public string SecondaryText => Loc.Get("Footer_Retry");

    public string FooterHint => Step switch
    {
        WizardStep.Apps => Apps.SelectionSummary,
        WizardStep.Target when Target is not null && IsSource(Target) => Loc.Get("Target_SameDevice"),
        WizardStep.Install when Install.IsReady && Target?.IsReady != true => Loc.Get("Install_TargetMissing"),
        _ => "",
    };

    public bool HasFooterHint => FooterHint.Length > 0;

    // Concurrent execution stays allowed: "Install" runs for the whole transfer and the same
    // button must remain clickable as "Stop".
    [RelayCommand(AllowConcurrentExecutions = true, CanExecute = nameof(IsPrimaryEnabled))]
    private async Task PrimaryAsync()
    {
        switch (Step)
        {
            case WizardStep.Source when Source?.IsReady == true:
                Step = WizardStep.Apps;
                await Apps.LoadAsync(Source);
                break;
            case WizardStep.Apps:
                Step = WizardStep.Target;
                break;
            case WizardStep.Target:
                Step = WizardStep.Install;
                break;
            case WizardStep.Install when Install.IsRunning:
                Install.Cancel();
                break;
            case WizardStep.Install when Install.IsFinished:
                Install.Reset();
                Apps.ClearSelection();
                Step = WizardStep.Source;
                break;
            case WizardStep.Install when Target is not null:
                await Install.StartAsync(Apps.SelectedApps, Target);
                break;
        }

        RefreshAll();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SecondaryAsync()
    {
        if (!Account.IsSignedIn)
            Install.PrepareRetryAfterSignIn();
        else
            await Install.RetryAsync();
        RefreshAll();
    }

    [RelayCommand]
    private void Back()
    {
        Step = Step switch
        {
            WizardStep.Apps => WizardStep.Source,
            WizardStep.Target => WizardStep.Apps,
            WizardStep.Install when Install.IsReady => WizardStep.Target,
            _ => Step,
        };
        RefreshAll();
    }

    [RelayCommand]
    private void ChooseDevice(DeviceItemViewModel? device)
    {
        if (device is null || !device.IsReady)
            return;
        if (IsSourceStep)
            Source = device;
        else if (IsTargetStep && !IsSource(device))
            Target = device;
        RefreshAll();
    }

    [RelayCommand]
    private async Task RetryApps()
    {
        if (Source is not null)
            await Apps.LoadAsync(Source);
    }

    partial void OnStepChanged(WizardStep value)
    {
        if (value == WizardStep.Target)
            AutoSelect();
        UpdateDeviceFlags();
    }

    private void OnDevicesChanged()
    {
        if (Source is not null && Devices.Find(Source.Udid) is null && IsSourceStep)
            Source = null;
        if (Target is not null && Devices.Find(Target.Udid) is null)
            Target = null;
        AutoSelect();
        RefreshAll();
    }

    private void AutoSelect()
    {
        var ready = Devices.Devices.Where(d => d.IsReady).ToList();
        if (IsSourceStep && Source is null && ready.Count > 0)
            Source = ready[0];
        if (IsTargetStep && (Target is null || IsSource(Target)))
            Target = ready.FirstOrDefault(d => !IsSource(d));
    }

    private void UpdateDeviceFlags()
    {
        foreach (var device in Devices.Devices)
        {
            device.IsSourceDevice = IsTargetStep && IsSource(device);
            device.IsChosen = (IsSourceStep && device.Udid == Source?.Udid) || (IsTargetStep && device.Udid == Target?.Udid);
        }
    }

    /// <summary>Compares by UDID: a replugged iPhone comes back as a new list item.</summary>
    private bool IsSource(DeviceItemViewModel? device) => device is not null && device.Udid == Source?.Udid;

    private void OnChildChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == Install && e.PropertyName == nameof(InstallViewModel.State) && Install.IsFinished && Install.NeedsSignIn)
            Account.MarkSessionExpired();
        RefreshAll();
    }

    private void RefreshAll()
    {
        UpdateDeviceFlags();
        OnPropertyChanged(nameof(AppsSubtitle));
        OnPropertyChanged(nameof(InstallTitle));
        OnPropertyChanged(nameof(InstallSubtitle));
        OnPropertyChanged(nameof(InstallGlyph));
        OnPropertyChanged(nameof(InstallTone));
        OnPropertyChanged(nameof(InstallRingProgress));
        OnPropertyChanged(nameof(InstallGlyphIsSuccess));
        OnPropertyChanged(nameof(InstallGlyphIsWarning));
        OnPropertyChanged(nameof(InstallGlyphIsNormal));
        OnPropertyChanged(nameof(ShowSignInPanel));
        OnPropertyChanged(nameof(ShowInstallSummary));
        OnPropertyChanged(nameof(SummaryFrom));
        OnPropertyChanged(nameof(SummaryTo));
        OnPropertyChanged(nameof(SummaryApps));
        OnPropertyChanged(nameof(ShowBack));
        OnPropertyChanged(nameof(PrimaryText));
        OnPropertyChanged(nameof(IsPrimaryEnabled));
        OnPropertyChanged(nameof(ShowPrimary));
        OnPropertyChanged(nameof(ShowSecondary));
        OnPropertyChanged(nameof(FooterHint));
        OnPropertyChanged(nameof(HasFooterHint));
        PrimaryCommand.NotifyCanExecuteChanged();
    }
}
