using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppBridge.Core.Library;
using AppBridge.Services;
using Microsoft.UI.Dispatching;

namespace AppBridge.ViewModels;

public enum WizardStep
{
    /// <summary>"Transfer Now" or "Transfer Later".</summary>
    Start,

    /// <summary>Transfer Later: save apps, or install saved apps.</summary>
    Later,
    Source,
    Apps,
    Target,
    Install,
}

/// <summary>What the steps after the start screen are doing.</summary>
public enum Flow
{
    /// <summary>Both iPhones connected: download each app and install it right away.</summary>
    Now,

    /// <summary>Old iPhone only: save its apps on this PC.</summary>
    Save,

    /// <summary>New iPhone only: install apps saved on this PC.</summary>
    InstallSaved,
}

/// <summary>
/// The whole app: the start screen, then one of three short flows built from the same steps.
/// Owns the shared footer (Back / primary button) so every step behaves the same way.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private static readonly WizardStep[] NowSteps = [WizardStep.Source, WizardStep.Apps, WizardStep.Target, WizardStep.Install];
    private static readonly WizardStep[] SaveSteps = [WizardStep.Source, WizardStep.Apps, WizardStep.Install];
    private static readonly WizardStep[] InstallSavedSteps = [WizardStep.Target, WizardStep.Apps, WizardStep.Install];

    public MainViewModel(AppServices services, DispatcherQueue dispatcher)
    {
        Services = services;
        Icons = new IconCache(services.IconsDirectory);
        Devices = new DeviceListViewModel(services.Watcher, services.Devices, dispatcher);
        Apps = new AppsViewModel(services.Devices, Icons, dispatcher);
        Saved = new SavedAppsViewModel(services.Devices, Icons);
        Library = new LibraryViewModel(services);
        Account = new AccountViewModel(services.AppStore);
        Install = new InstallViewModel(services, Icons, dispatcher);

        Devices.StateChanged += (_, _) => OnDevicesChanged();
        Apps.PropertyChanged += OnChildChanged;
        Saved.PropertyChanged += OnChildChanged;
        Library.PropertyChanged += OnChildChanged;
        Account.PropertyChanged += OnChildChanged;
        Install.PropertyChanged += OnChildChanged;
        _ = Library.RefreshAsync();
    }

    public AppServices Services { get; }

    public IconCache Icons { get; }

    public DeviceListViewModel Devices { get; }

    public AppsViewModel Apps { get; }

    public SavedAppsViewModel Saved { get; }

    public LibraryViewModel Library { get; }

    public AccountViewModel Account { get; }

    public InstallViewModel Install { get; }

    public bool IsToolMissing => !Services.AppStore.IsAvailable;

    [ObservableProperty]
    public partial WizardStep Step { get; set; }

    [ObservableProperty]
    public partial Flow Flow { get; set; }

    [ObservableProperty]
    public partial DeviceItemViewModel? Source { get; set; }

    [ObservableProperty]
    public partial DeviceItemViewModel? Target { get; set; }

    public bool IsStartStep => Step == WizardStep.Start;

    public bool IsLaterStep => Step == WizardStep.Later;

    public bool IsSourceStep => Step == WizardStep.Source;

    public bool IsAppsStep => Step == WizardStep.Apps;

    public bool IsTargetStep => Step == WizardStep.Target;

    public bool IsInstallStep => Step == WizardStep.Install;

    /// <summary>Source and target steps share one device list view.</summary>
    public bool IsDeviceStep => Step is WizardStep.Source or WizardStep.Target;

    private WizardStep[] FlowSteps => Flow switch
    {
        Flow.Save => SaveSteps,
        Flow.InstallSaved => InstallSavedSteps,
        _ => NowSteps,
    };

    /// <summary>Page dots: one per step of the current flow, none on the start screens.</summary>
    public int DotCount => Step is WizardStep.Start or WizardStep.Later ? 0 : FlowSteps.Length;

    public int DotIndex => Array.IndexOf(FlowSteps, Step);

    // Start and Later --------------------------------------------------------------------

    public bool CanDownload => !IsToolMissing;

    public string NowSubtitle => IsToolMissing ? Loc.Get("Choice_ToolMissing") : Loc.Get("Start_NowSubtitle");

    public string LaterSubtitle => Library.HasApps ? Loc.Plural("Start_SavedCount", Library.Count) : Loc.Get("Start_LaterSubtitle");

    public bool LaterSubtitleIsAccent => Library.HasApps;

    public string SaveSubtitle => IsToolMissing ? Loc.Get("Choice_ToolMissing") : Loc.Get("Later_SaveSubtitle");

    public bool CanInstallSaved => Library.HasApps && Library.FolderExists;

    public string InstallSavedSubtitle =>
        !Library.FolderExists && !string.IsNullOrEmpty(Services.Settings.Current.DownloadDirectory) ? Loc.Get("Later_FolderMissing")
        : Library.HasApps ? $"{Loc.Plural("Apps_Count", Library.Count)}  ·  {Format.Bytes(Library.TotalBytes)}"
        : Loc.Get("Later_SaveFirst");

    // Device steps -----------------------------------------------------------------------

    public string DeviceStepTitle => (Flow, Step) switch
    {
        (Flow.Save, WizardStep.Source) => Loc.Get("Old_Title"),
        (Flow.InstallSaved, WizardStep.Target) => Loc.Get("New_Title"),
        (_, WizardStep.Target) => Loc.Get("Target_Title"),
        _ => Loc.Get("Source_Title"),
    };

    public string DeviceStepSubtitle => (Flow, Step) switch
    {
        (Flow.InstallSaved, WizardStep.Target) => Loc.Get("New_Subtitle"),
        (_, WizardStep.Target) => Loc.Get("Target_Subtitle"),
        _ => Loc.Get("Source_Subtitle"),
    };

    public string DeviceStepTone => Flow switch
    {
        Flow.Save => "Indigo",
        Flow.InstallSaved => "Teal",
        _ => IsTargetStep ? "Teal" : "Blue",
    };

    // Apps step --------------------------------------------------------------------------

    /// <summary>The apps on the source iPhone, or the apps saved on this PC when installing later.</summary>
    public AppListViewModel AppList => Flow == Flow.InstallSaved ? Saved : Apps;

    public string AppsSubtitle => Flow switch
    {
        Flow.InstallSaved => Loc.Format("Saved_Subtitle", Target?.Name ?? "iPhone"),
        Flow.Save => Source is null ? "" : Loc.Format("Apps_SubtitleSave", Source.Name),
        _ => Source is null ? "" : Loc.Format("Apps_Subtitle", Source.Name),
    };

    // Install step -----------------------------------------------------------------------

    private bool NeedsSignIn => Flow != Flow.InstallSaved;

    private int SelectedCount => Flow == Flow.InstallSaved ? Saved.SelectedCount : Apps.SelectedCount;

    private bool AllDone => Install.FailedCount == 0 && !Install.WasCanceled;

    public string InstallTitle => (Flow, Install.State) switch
    {
        (Flow.Save, InstallState.Running) => Loc.Get("Save_Title_Running"),
        (Flow.Save, InstallState.Finished) when Install.WasCanceled => Loc.Get("Save_Title_Stopped"),
        (Flow.Save, InstallState.Finished) when Install.FailedCount == 0 => Loc.Get("Save_Title_Done"),
        (Flow.Save, InstallState.Finished) => Loc.Get("Save_Title_Problems"),
        (_, InstallState.Running) => Loc.Get("Install_Title_Running"),
        (_, InstallState.Finished) when Install.WasCanceled => Loc.Get("Install_Title_Stopped"),
        (_, InstallState.Finished) when Install.FailedCount == 0 => Loc.Get("Install_Title_Done"),
        (_, InstallState.Finished) => Loc.Get("Install_Title_Problems"),
        _ when NeedsSignIn && !Account.IsSignedIn => Loc.Get("Install_Title_SignIn"),
        (Flow.Save, _) => Loc.Get("Save_Title_Ready"),
        _ => Loc.Get("Install_Title_Ready"),
    };

    public string InstallSubtitle => (Flow, Install.State) switch
    {
        (Flow.Save, InstallState.Running) => Loc.Get("Save_Subtitle_Running"),
        (Flow.Save, InstallState.Finished) when AllDone => Loc.Format("Save_Subtitle_Done", Loc.Plural("Apps_Count", Install.CompletedCount)),
        (Flow.Save, InstallState.Finished) => Loc.Format("Save_Subtitle_Problems", Install.CompletedCount, Install.Items.Count - Install.CompletedCount),
        (_, InstallState.Running) => Loc.Format("Install_Subtitle_Running", Install.CompletedCount + Install.FailedCount, Install.Items.Count, Install.TargetName),
        (_, InstallState.Finished) when AllDone => Loc.Format("Install_Subtitle_Done", Loc.Plural("Apps_Count", Install.CompletedCount), Install.TargetName),
        (_, InstallState.Finished) => Loc.Format("Install_Subtitle_Problems", Install.CompletedCount, Install.Items.Count - Install.CompletedCount),
        _ when NeedsSignIn && !Account.IsSignedIn => Loc.Get(Flow == Flow.Save ? "Save_Subtitle_SignIn" : "Install_Subtitle_SignIn"),
        (Flow.Save, _) => Loc.Format("Save_Subtitle_Ready", Loc.Plural("Apps_Count", SelectedCount)),
        (Flow.InstallSaved, _) => Loc.Format("InstallSaved_Subtitle_Ready", Loc.Plural("Apps_Count", SelectedCount), Target?.Name ?? "iPhone"),
        _ => Loc.Format("Install_Subtitle_Ready", Loc.Plural("Apps_Count", SelectedCount), Target?.Name ?? "iPhone"),
    };

    public string InstallGlyph => Install.State switch
    {
        InstallState.Finished when AllDone => "",
        InstallState.Finished => "",
        _ when NeedsSignIn && !Account.IsSignedIn && Install.IsReady => "",
        _ when Flow == Flow.InstallSaved => "",
        _ => "",
    };

    public string InstallTone => Install.State switch
    {
        InstallState.Finished when AllDone => "Green",
        InstallState.Finished => "Orange",
        _ when NeedsSignIn && !Account.IsSignedIn && Install.IsReady => "Indigo",
        _ => Flow switch
        {
            Flow.Save => "Indigo",
            Flow.InstallSaved => "Teal",
            _ => "Blue",
        },
    };

    /// <summary>Overall progress for the ring around the hero badge; -1 hides the ring.</summary>
    public double InstallRingProgress => Install.IsRunning ? Install.OverallProgress : -1;

    public bool InstallGlyphIsSuccess => Install.IsFinished && AllDone;

    public bool InstallGlyphIsWarning => Install.IsFinished && !AllDone;

    public bool InstallGlyphIsNormal => !InstallGlyphIsSuccess && !InstallGlyphIsWarning;

    public bool ShowSignInPanel => IsInstallStep && Install.IsReady && NeedsSignIn && !Account.IsSignedIn;

    public bool ShowInstallSummary => IsInstallStep && Install.IsReady && (!NeedsSignIn || Account.IsSignedIn);

    public bool ShowOptions => Flow != Flow.InstallSaved;

    public bool ShowKeepOption => Flow == Flow.Now;

    public bool ShowSummaryTo => Flow != Flow.Save;

    public bool ShowSummaryFolder => Flow == Flow.Save;

    public bool ShowSignOut => NeedsSignIn;

    public string SummaryFrom => Flow == Flow.InstallSaved ? Loc.Get("Summary_ThisPc") : Source?.Name ?? "—";

    public string SummaryTo => Target?.Name ?? "—";

    public string SummaryApps => Loc.Plural("Apps_Count", SelectedCount);

    public string SummaryAccount => Flow == Flow.InstallSaved
        ? SavedAccounts.Count switch
        {
            0 => "—",
            1 => SavedAccounts[0],
            var n => Loc.Plural("Summary_Accounts", n),
        }
        : Account.Email ?? "";

    public string InstallFootnote => Flow switch
    {
        Flow.InstallSaved => Loc.Format("InstallSaved_Footnote", SavedAccountPhrase),
        Flow.Save => Loc.Get("Save_Footnote"),
        _ => Loc.Get("Now_Footnote"),
    };

    /// <summary>"When your new iPhone arrives": shown after apps were saved.</summary>
    public bool ShowSaveDoneCard => IsInstallStep && Flow == Flow.Save && Install.IsFinished && Install.CompletedCount > 0;

    public string SaveDoneStep1 => Loc.Format("SaveDone_Step1", string.IsNullOrEmpty(Account.Email) ? Loc.Get("SaveDone_YourAccount") : Account.Email);

    public string SaveDoneFootnote => Loc.Format("SaveDone_Footnote", Library.FolderName);

    /// <summary>The Apple Account reminder after installing saved apps.</summary>
    public bool ShowAccountCard => IsInstallStep && Flow == Flow.InstallSaved && Install.IsFinished && Install.CompletedCount > 0;

    public string AccountCardText => Loc.Format("InstallSaved_AccountCard", SavedAccountPhrase);

    public string FolderDisplay => Library.FolderDisplay;

    private List<string> SavedAccounts => Saved.SelectedApps
        .Select(a => a.PurchaserAppleId).Where(a => !string.IsNullOrEmpty(a)).Select(a => a!)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private string SavedAccountPhrase => SavedAccounts.Count == 1 ? SavedAccounts[0] : Loc.Get("InstallSaved_TheirAccount");

    // Footer -----------------------------------------------------------------------------

    public bool ShowBack => Step != WizardStep.Start && !(IsInstallStep && !Install.IsReady);

    public string PrimaryText => Step switch
    {
        WizardStep.Install => Install.State switch
        {
            InstallState.Running => Loc.Get("Footer_Stop"),
            InstallState.Finished => Loc.Get("Footer_Done"),
            _ => Loc.Get(Flow == Flow.Save ? "Footer_Save" : "Footer_Install"),
        },
        _ => Loc.Get("Footer_Continue"),
    };

    public bool IsPrimaryEnabled => Step switch
    {
        WizardStep.Source => Source?.IsReady == true,
        WizardStep.Apps => AppList.HasSelection && !AppList.IsLoading,
        WizardStep.Target => Target?.IsReady == true && (Flow == Flow.InstallSaved || !IsSource(Target)),
        WizardStep.Install => Install.State != InstallState.Ready || Flow switch
        {
            Flow.Save => Account.IsSignedIn,
            Flow.InstallSaved => Target?.IsReady == true,
            _ => Account.IsSignedIn && Target?.IsReady == true,
        },
        _ => false,
    };

    /// <summary>The start screens navigate by their rows; the sign-in form has its own button.</summary>
    public bool ShowPrimary => Step is not (WizardStep.Start or WizardStep.Later) && !ShowSignInPanel;

    public bool ShowSecondary => IsInstallStep && Install.IsFinished && Install.RetryableCount > 0;

    public string SecondaryText => Loc.Get("Footer_Retry");

    public string FooterHint => Step switch
    {
        WizardStep.Start => Loc.Get("Start_Footer"),
        WizardStep.Apps => AppList.SelectionSummary,
        WizardStep.Target when Flow == Flow.Now && Target is not null && IsSource(Target) => Loc.Get("Target_SameDevice"),
        WizardStep.Install when Install.IsReady && Flow != Flow.Save && Target?.IsReady != true => Loc.Get("Install_TargetMissing"),
        _ => "",
    };

    public bool HasFooterHint => FooterHint.Length > 0;

    // Commands ---------------------------------------------------------------------------

    [RelayCommand]
    private void TransferNow()
    {
        if (IsToolMissing)
            return;
        Begin(Flow.Now, WizardStep.Source);
    }

    [RelayCommand]
    private async Task TransferLater()
    {
        Step = WizardStep.Later;
        RefreshAll();
        await Library.RefreshAsync();
    }

    [RelayCommand]
    private void SaveApps()
    {
        if (IsToolMissing)
            return;
        Begin(Flow.Save, WizardStep.Source);
    }

    [RelayCommand]
    private void InstallSavedApps()
    {
        if (!CanInstallSaved)
            return;
        Source = null;
        Begin(Flow.InstallSaved, WizardStep.Target);
    }

    [RelayCommand]
    private void OpenFolder() => Library.OpenFolder();

    // Concurrent execution stays allowed: "Install" runs for the whole transfer and the same
    // button must remain clickable as "Stop".
    [RelayCommand(AllowConcurrentExecutions = true, CanExecute = nameof(IsPrimaryEnabled))]
    private async Task PrimaryAsync()
    {
        switch (Step)
        {
            case WizardStep.Source when Source?.IsReady == true:
                Step = WizardStep.Apps;
                var load = Apps.LoadAsync(Source);
                if (Flow == Flow.Save)
                    await Library.RefreshAsync();
                await load;
                Apps.ShowSaved(Flow == Flow.Save ? Library.Scan : null);
                break;
            case WizardStep.Target when Flow == Flow.InstallSaved && Target is not null:
                Step = WizardStep.Apps;
                await LoadSavedAsync();
                break;
            case WizardStep.Target:
                Step = WizardStep.Install;
                break;
            case WizardStep.Apps:
                Step = Flow == Flow.Now ? WizardStep.Target : WizardStep.Install;
                break;
            case WizardStep.Install when Install.IsRunning:
                Install.Cancel();
                break;
            case WizardStep.Install when Install.IsFinished:
                Finish();
                break;
            case WizardStep.Install when Flow == Flow.Save:
                await Install.StartSaveAsync(Apps.SelectedApps);
                await Library.RefreshAsync();
                break;
            case WizardStep.Install when Flow == Flow.InstallSaved && Target is not null:
                await Install.StartInstallSavedAsync(Saved.SelectedApps, Target);
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
        if (NeedsSignIn && !Account.IsSignedIn)
            Install.PrepareRetryAfterSignIn();
        else
            await Install.RetryAsync();
        if (Flow == Flow.Save)
            await Library.RefreshAsync();
        RefreshAll();
    }

    [RelayCommand]
    private void Back()
    {
        // Leaving the install step drops a pending "retry after sign-in": the selection may change.
        if (IsInstallStep && Install.IsReady)
            Install.Reset();
        Step = (Flow, Step) switch
        {
            (_, WizardStep.Later) => WizardStep.Start,
            (Flow.Now, WizardStep.Source) => WizardStep.Start,
            (Flow.Save, WizardStep.Source) or (Flow.InstallSaved, WizardStep.Target) => WizardStep.Later,
            (Flow.InstallSaved, WizardStep.Apps) => WizardStep.Target,
            (_, WizardStep.Apps) => WizardStep.Source,
            (Flow.Now, WizardStep.Target) => WizardStep.Apps,
            (Flow.Now, WizardStep.Install) when Install.IsReady => WizardStep.Target,
            (_, WizardStep.Install) when Install.IsReady => WizardStep.Apps,
            _ => Step,
        };
        RefreshAll();
        if (Step is WizardStep.Start or WizardStep.Later)
            _ = Library.RefreshAsync();
    }

    [RelayCommand]
    private void ChooseDevice(DeviceItemViewModel? device)
    {
        if (device is null || !device.IsReady)
            return;
        if (IsSourceStep)
            Source = device;
        else if (IsTargetStep && (Flow == Flow.InstallSaved || !IsSource(device)))
            Target = device;
        RefreshAll();
    }

    [RelayCommand]
    private async Task RetryApps()
    {
        if (Flow == Flow.InstallSaved)
            await LoadSavedAsync();
        else if (Source is not null)
        {
            await Apps.LoadAsync(Source);
            Apps.ShowSaved(Flow == Flow.Save ? Library.Scan : null);
        }
    }

    private void Begin(Flow flow, WizardStep first)
    {
        Install.Reset();
        Apps.ClearSelection();
        Saved.Reset();
        Flow = flow;
        Step = first;
        AutoSelect();
        RefreshAll();
    }

    /// <summary>"Done": Transfer Now returns to the start screen, the later flows to their hub.</summary>
    private void Finish()
    {
        Install.Reset();
        Apps.ClearSelection();
        Saved.Reset();
        Step = Flow == Flow.Now ? WizardStep.Start : WizardStep.Later;
        _ = Library.RefreshAsync();
    }

    private async Task LoadSavedAsync()
    {
        // The iPhone may be unplugged while the folder is scanned; keep the one the user chose.
        var target = Target;
        if (target is null)
            return;
        Saved.ShowLoading();
        await Library.RefreshAsync();
        if (Step != WizardStep.Apps || Flow != Flow.InstallSaved)
            return;
        await Saved.LoadAsync(target, Library.Scan, Library.Folder);
        RefreshAll();
    }

    partial void OnStepChanged(WizardStep value)
    {
        if (value is WizardStep.Source or WizardStep.Target)
            AutoSelect();
        RefreshAll();
    }

    partial void OnFlowChanged(Flow value) => OnPropertyChanged(nameof(AppList));

    private string? _sourceUdid;
    private string? _targetUdid;

    partial void OnSourceChanged(DeviceItemViewModel? value)
    {
        if (value is not null)
            _sourceUdid = value.Udid;
    }

    partial void OnTargetChanged(DeviceItemViewModel? value)
    {
        if (value is not null)
            _targetUdid = value.Udid;
    }

    private void OnDevicesChanged()
    {
        if (Source is not null && Devices.Find(Source.Udid) is null && IsSourceStep)
            Source = null;
        if (Target is not null && Devices.Find(Target.Udid) is null)
            Target = null;
        if (Target is null && Step is WizardStep.Apps or WizardStep.Install && _targetUdid is not null && Flow != Flow.Save
            && Devices.Find(_targetUdid) is { IsReady: true } back)
            Target = back;
        if (Source is null && Step is WizardStep.Apps or WizardStep.Install && _sourceUdid is not null && Flow != Flow.InstallSaved
            && Devices.Find(_sourceUdid) is { IsReady: true } again)
            Source = again;
        AutoSelect();
        RefreshAll();
    }

    private void AutoSelect()
    {
        var ready = Devices.Devices.Where(d => d.IsReady).ToList();
        if (IsSourceStep && Source is null && ready.Count > 0)
            Source = ready[0];
        // With several iPhones plugged in, the new one is most likely the one on the newest iOS.
        if (IsTargetStep && Flow == Flow.InstallSaved && Target is null)
            Target = ready.OrderByDescending(d => d.OsVersion, Comparer<string>.Create(SavedAppLibrary.CompareVersions)).FirstOrDefault();
        else if (IsTargetStep && (Target is null || IsSource(Target)))
            Target = ready.FirstOrDefault(d => !IsSource(d));
    }

    private void UpdateDeviceFlags()
    {
        foreach (var device in Devices.Devices)
        {
            device.IsSourceDevice = IsTargetStep && Flow == Flow.Now && IsSource(device);
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

    private static readonly string[] Derived =
    [
        nameof(IsStartStep), nameof(IsLaterStep), nameof(IsSourceStep), nameof(IsAppsStep), nameof(IsTargetStep), nameof(IsInstallStep),
        nameof(IsDeviceStep), nameof(AppList), nameof(DotCount), nameof(DotIndex), nameof(CanDownload), nameof(NowSubtitle), nameof(LaterSubtitle), nameof(LaterSubtitleIsAccent),
        nameof(SaveSubtitle), nameof(CanInstallSaved), nameof(InstallSavedSubtitle), nameof(FolderDisplay),
        nameof(DeviceStepTitle), nameof(DeviceStepSubtitle), nameof(DeviceStepTone), nameof(AppsSubtitle),
        nameof(InstallTitle), nameof(InstallSubtitle), nameof(InstallGlyph), nameof(InstallTone), nameof(InstallRingProgress),
        nameof(InstallGlyphIsSuccess), nameof(InstallGlyphIsWarning), nameof(InstallGlyphIsNormal),
        nameof(ShowSignInPanel), nameof(ShowInstallSummary), nameof(ShowOptions), nameof(ShowKeepOption), nameof(ShowSummaryTo),
        nameof(ShowSummaryFolder), nameof(ShowSignOut), nameof(SummaryFrom), nameof(SummaryTo), nameof(SummaryApps), nameof(SummaryAccount),
        nameof(InstallFootnote), nameof(ShowSaveDoneCard), nameof(SaveDoneStep1), nameof(SaveDoneFootnote), nameof(ShowAccountCard),
        nameof(AccountCardText), nameof(ShowBack), nameof(PrimaryText), nameof(IsPrimaryEnabled), nameof(ShowPrimary),
        nameof(ShowSecondary), nameof(FooterHint), nameof(HasFooterHint),
    ];

    private void RefreshAll()
    {
        UpdateDeviceFlags();
        foreach (var name in Derived)
            OnPropertyChanged(name);
        PrimaryCommand.NotifyCanExecuteChanged();
    }
}
