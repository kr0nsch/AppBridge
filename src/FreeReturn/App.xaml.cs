using FreeReturn.Core;
using FreeReturn.Services;
using Microsoft.UI.Xaml;

namespace FreeReturn;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        Instance = this;
        Services = AppServices.Create();

        // Must be set before any resource is loaded.
        if (!string.IsNullOrEmpty(Services.Settings.Current.Language))
        {
            try
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = Services.Settings.Current.Language;
            }
            catch (Exception ex)
            {
                AppLog.Warn("Could not apply the selected language.", ex);
            }
        }

        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLog.Error("Unhandled exception.", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error("Unobserved task exception.", e.Exception);
            e.SetObserved();
        };

        InitializeComponent();
    }

    public static App? Instance { get; private set; }

    public AppServices Services { get; }

    public MainWindow? Window => _window;

    public void BringToFront() => _window?.DispatcherQueue.TryEnqueue(() => _window.BringToFront());

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppLog.Info($"FreeReturn {AppServices.Version} started ({(Services.Paths.IsPortable ? "portable" : "installed")}).");
        _window = new MainWindow(Services);
        _window.Closed += (_, _) => Services.Dispose();
        _window.Activate();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        AppLog.Error("Unhandled UI exception.", e.Exception);
        e.Handled = true;
        _window?.ShowError(e.Exception);
    }
}
