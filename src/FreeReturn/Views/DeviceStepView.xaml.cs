using FreeReturn.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace FreeReturn.Views;

public sealed partial class DeviceStepView : UserControl
{
    public DeviceStepView(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            // Decorative motion only when Windows "Animation effects" is on.
            if (new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
                RadarPulse.Begin();
        };
    }

    public MainViewModel ViewModel { get; }

    private void OnDeviceClick(object sender, ItemClickEventArgs e) => ViewModel.ChooseDeviceCommand.Execute(e.ClickedItem as DeviceItemViewModel);

    private void OnHelpClick(object sender, RoutedEventArgs e) => FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);

    private async void OnGetAppleDevicesClick(object sender, RoutedEventArgs e) =>
        await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?ProductId=9NP83LWLPZ9K"));
}
