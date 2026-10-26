using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AppBridge.Core;
using AppBridge.Core.Devices;
using AppBridge.Core.Library;
using AppBridge.Core.Transfer;
using AppBridge.Services;
using Microsoft.UI.Dispatching;

namespace AppBridge.ViewModels;

public enum InstallState
{
    Ready,
    Running,
    Finished,
}

/// <summary>
/// The last step of every flow: download and install (Transfer Now), download only (saving for
/// later) or install saved files (installing later), with per-app progress.
/// </summary>
public sealed partial class InstallViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IconCache _icons;
    private readonly DispatcherQueue _dispatcher;
    private readonly ConcurrentDictionary<string, TransferUpdate> _lastUpdate = new();
    private readonly Dictionary<string, SavedApp> _saved = new(StringComparer.Ordinal);
    private CancellationTokenSource? _run;
    private string? _targetUdid;

    public InstallViewModel(AppServices services, IconCache icons, DispatcherQueue dispatcher)
    {
        _services = services;
        _icons = icons;
        _dispatcher = dispatcher;
    }

    public ObservableCollection<TransferItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady), nameof(IsRunning), nameof(IsFinished), nameof(ShowItems))]
    public partial InstallState State { get; set; }

    [ObservableProperty]
    public partial double OverallProgress { get; set; }

    [ObservableProperty]
    public partial int CompletedCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailures))]
    public partial int FailedCount { get; set; }

    [ObservableProperty]
    public partial bool WasCanceled { get; set; }

    [ObservableProperty]
    public partial string TargetName { get; set; } = "iPhone";

    /// <summary>Which kind of run the items belong to.</summary>
    public Flow Flow { get; private set; }

    public bool IsReady => State == InstallState.Ready;

    public bool IsRunning => State == InstallState.Running;

    public bool IsFinished => State == InstallState.Finished;

    public bool ShowItems => State != InstallState.Ready;

    public bool HasFailures => FailedCount > 0;

    public int RetryableCount => Items.Count(i => i.IsFailed || i.Stage == TransferStage.Canceled);

    /// <summary>Apple ended the session during the run; the user must sign in before retrying.</summary>
    public bool NeedsSignIn => Items.Any(i => i.Failure == TransferFailure.NotSignedIn);

    private bool _retryPending;

    public bool UseLatestVersion
    {
        get => _services.Settings.Current.VersionPolicy == VersionPolicy.Latest;
        set => UpdateSetting(s => s.VersionPolicy = value ? VersionPolicy.Latest : VersionPolicy.SameAsSource);
    }

    public bool UseSourceVersion
    {
        get => !UseLatestVersion;
        set => UseLatestVersion = !value;
    }

    public bool AcquireFreeLicenses
    {
        get => _services.Settings.Current.AcquireFreeLicenses;
        set => UpdateSetting(s => s.AcquireFreeLicenses = value);
    }

    public bool KeepDownloads
    {
        get => _services.Settings.Current.KeepDownloads;
        set => UpdateSetting(s => s.KeepDownloads = value);
    }

    /// <summary>Transfer Now: download from the App Store and install on <paramref name="target"/>.</summary>
    public Task StartAsync(IReadOnlyList<InstalledApp> apps, DeviceItemViewModel target) => BeginAsync(Flow.Now, apps, target);

    /// <summary>Transfer Later, part one: download into the saved-apps folder.</summary>
    public Task StartSaveAsync(IReadOnlyList<InstalledApp> apps) => BeginAsync(Flow.Save, apps, null);

    /// <summary>Transfer Later, part two: install saved files on <paramref name="target"/>; no sign-in needed.</summary>
    public Task StartInstallSavedAsync(IReadOnlyList<SavedApp> apps, DeviceItemViewModel target)
    {
        _saved.Clear();
        foreach (var app in apps)
            _saved[app.BundleId] = app;
        return BeginAsync(Flow.InstallSaved, apps.Select(a => a.ToInstalledApp()).ToList(), target);
    }

    public Task RetryAsync()
    {
        var retry = Items.Where(i => i.IsFailed || i.Stage == TransferStage.Canceled).Select(i => i.App).ToList();
        return retry.Count == 0 || (_targetUdid is null && Flow != Flow.Save) ? Task.CompletedTask : RunAsync(retry);
    }

    private async Task BeginAsync(Flow flow, IReadOnlyList<InstalledApp> apps, DeviceItemViewModel? target)
    {
        _targetUdid = target?.Udid;
        TargetName = target?.Name ?? "iPhone";
        if (_retryPending && Flow == flow)
        {
            _retryPending = false;
            await RetryAsync();
            return;
        }

        _retryPending = false;
        Flow = flow;
        // Busy from the first moment, so a second click can't start another run while icons load.
        State = InstallState.Running;
        Items.Clear();
        _lastUpdate.Clear();
        for (var i = 0; i < apps.Count; i++)
        {
            var icon = await _icons.GetAsync(apps[i].BundleId);
            Items.Add(new TransferItemViewModel(apps[i], icon, flow) { ShowSeparator = i > 0 });
        }

        await RunAsync(apps);
    }

    public void Cancel() => _run?.Cancel();

    /// <summary>Goes back to the sign-in screen; the next Install retries only the unfinished apps.</summary>
    public void PrepareRetryAfterSignIn()
    {
        _retryPending = true;
        State = InstallState.Ready;
    }

    public void Reset()
    {
        _retryPending = false;
        _run?.Cancel();
        Items.Clear();
        State = InstallState.Ready;
        OverallProgress = 0;
        CompletedCount = 0;
        FailedCount = 0;
        WasCanceled = false;
    }

    private async Task RunAsync(IReadOnlyList<InstalledApp> apps)
    {
        _run = new CancellationTokenSource();
        State = InstallState.Running;
        WasCanceled = false;
        Recount();

        var options = new TransferOptions(
            _services.DownloadDirectory,
            _services.Settings.Current.KeepDownloads,
            _services.Settings.Current.VersionPolicy,
            _services.Settings.Current.AcquireFreeLicenses);

        AppLog.Info($"{Flow}: {apps.Count} app(s).");
        KeepAwake.Enable();
        try
        {
            switch (Flow)
            {
                case Flow.Save:
                    Directory.CreateDirectory(options.DownloadDirectory);
                    await _services.Transfers.SaveAsync(apps, options with { KeepDownloads = true }, Report, _run.Token);
                    break;
                case Flow.InstallSaved:
                    await _services.Transfers.InstallSavedAsync(apps.Select(a => _saved[a.BundleId]).ToList(), _targetUdid!, Report, _run.Token);
                    break;
                default:
                    await _services.Transfers.RunAsync(apps, _targetUdid!, options, Report, _run.Token);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            WasCanceled = true;
            AppLog.Info("Installation stopped by the user.");
        }
        catch (Exception ex)
        {
            AppLog.Error("The installation run failed.", ex);
            foreach (var item in Items.Where(i => !i.IsCompleted && !i.IsFailed))
                item.Apply(new TransferUpdate(TransferStage.Failed, Failure: TransferFailure.Unknown, Detail: ex.Message));
        }
        finally
        {
            KeepAwake.Disable();
        }

        // Let the last queued progress updates land before summarising.
        await Task.Delay(150);
        Recount();
        State = InstallState.Finished;
        AppLog.Info($"Finished: {CompletedCount} done, {FailedCount} failed.");
    }

    private void Report(InstalledApp app, TransferUpdate update)
    {
        // Progress arrives very often; only forward visible changes to the UI thread.
        if (_lastUpdate.TryGetValue(app.BundleId, out var last) && last.Stage == update.Stage
            && Math.Abs((last.Fraction ?? 0) - (update.Fraction ?? 0)) < 0.005
            && Math.Abs(last.BytesReceived - update.BytesReceived) < 2_000_000)
            return;
        _lastUpdate[app.BundleId] = update;

        _dispatcher.TryEnqueue(() =>
        {
            var item = Items.FirstOrDefault(i => i.App.BundleId == app.BundleId);
            if (item is null)
                return;
            item.Apply(update);
            Recount();
        });
    }

    private void Recount()
    {
        CompletedCount = Items.Count(i => i.IsCompleted);
        FailedCount = Items.Count(i => i.IsFailed);
        OverallProgress = Items.Count == 0 ? 0 : Items.Sum(i => i.OverallFraction) / Items.Count * 100;
    }

    private void UpdateSetting(Action<AppSettings> change)
    {
        change(_services.Settings.Current);
        _services.Settings.Save();
        OnPropertyChanged(string.Empty);
    }
}
