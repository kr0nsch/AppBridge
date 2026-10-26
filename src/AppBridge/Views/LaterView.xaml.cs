using AppBridge.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AppBridge.Views;

/// <summary>Transfer Later: save apps from the old iPhone, or install saved apps on the new one.</summary>
public sealed partial class LaterView : UserControl
{
    public LaterView(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // With apps saved, installing them is the likely next step: put keyboard focus there
        // when the hub opens or the row becomes available, not on every refresh.
        Loaded += (_, _) => UpdateFocus();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.CanInstallSaved) or nameof(MainViewModel.IsLaterStep))
                UpdateFocus();
        };
    }

    private bool _focusable;

    private void UpdateFocus()
    {
        var focusable = ViewModel.IsLaterStep && ViewModel.CanInstallSaved;
        if (focusable && !_focusable && IsLoaded)
            DispatcherQueue.TryEnqueue(InstallRow.FocusRow);
        _focusable = focusable && IsLoaded;
    }

    public MainViewModel ViewModel { get; }

    private async void OnChangeFolderClick(object sender, RoutedEventArgs e)
    {
        if (App.Instance?.Window is not { } window)
            return;
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
            return;
        ViewModel.Library.ChangeFolder(folder.Path);
        await ViewModel.Library.RefreshAsync();
    }
}
