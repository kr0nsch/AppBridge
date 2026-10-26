using System.Diagnostics;
using AppBridge.Core;
using AppBridge.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AppBridge.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    private static readonly (string Tag, string Label)[] Languages =
    [
        ("", "Settings_LanguageSystem"),
        ("en-US", "English"),
        ("ru-RU", "Русский"),
    ];

    private readonly AppServices _services;
    private readonly MainWindow _window;
    private readonly string _startupLanguage;
    private bool _loading = true;

    public SettingsDialog(AppServices services, MainWindow window)
    {
        _services = services;
        _window = window;
        _startupLanguage = services.Settings.Current.Language;
        InitializeComponent();

        foreach (var (tag, label) in Languages)
            LanguageBox.Items.Add(new ComboBoxItem { Tag = tag, Content = tag.Length == 0 ? Loc.Get(label) : label });
        LanguageBox.SelectedIndex = Math.Max(0, Array.FindIndex(Languages, l => l.Tag == services.Settings.Current.Language));

        (services.Settings.Current.Theme switch
        {
            ThemePreference.Light => ThemeLight,
            ThemePreference.Dark => ThemeDark,
            _ => ThemeSystem,
        }).IsChecked = true;

        KeepToggle.IsOn = services.Settings.Current.KeepDownloads;
        FolderText.Text = services.DownloadDirectory;
        VersionText.Text = AppServices.Version;
        _loading = false;
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageBox.SelectedItem is not ComboBoxItem { Tag: string tag })
            return;
        _services.Settings.Current.Language = tag;
        _services.Settings.Save();
        RestartPanel.Visibility = tag == _startupLanguage ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnThemeChecked(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        var theme = ReferenceEquals(sender, ThemeLight) ? ThemePreference.Light : ReferenceEquals(sender, ThemeDark) ? ThemePreference.Dark : ThemePreference.System;
        _services.Settings.Current.Theme = theme;
        _services.Settings.Save();
        _window.ApplyTheme(theme);
        RequestedTheme = _window.CurrentElementTheme;
    }

    private void OnKeepToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        _services.Settings.Current.KeepDownloads = KeepToggle.IsOn;
        _services.Settings.Save();
    }

    private async void OnChangeFolderClick(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
            return;
        _services.Settings.Current.DownloadDirectory = folder.Path;
        _services.Settings.Save();
        FolderText.Text = folder.Path;
        AppLog.Info("Download folder changed.");
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_services.DownloadDirectory);
        Shell.Open(_services.DownloadDirectory);
    }

    private void OnGitHubClick(object sender, RoutedEventArgs e) => Shell.Open(AppServices.RepositoryUrl);

    private void OnLicensesClick(object sender, RoutedEventArgs e)
    {
        var notices = Path.Combine(_services.Paths.AppDirectory, "THIRD-PARTY-NOTICES.txt");
        Shell.Open(File.Exists(notices) ? notices : $"{AppServices.RepositoryUrl}/blob/main/THIRD-PARTY-NOTICES.txt");
    }

    private void OnLogsClick(object sender, RoutedEventArgs e) => Shell.Open(_services.Paths.LogsDirectory);

    private void OnRestartClick(object sender, RoutedEventArgs e) => _window.Restart();
}

internal static class Shell
{
    public static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Could not open {target}.", ex);
        }
    }
}
