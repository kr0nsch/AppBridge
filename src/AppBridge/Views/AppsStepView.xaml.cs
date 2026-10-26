using AppBridge.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace AppBridge.Views;

public sealed partial class AppsStepView : UserControl
{
    public AppsStepView(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public MainViewModel ViewModel { get; }

    private void OnAppClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AppItemViewModel { IsSelectable: true } app)
            app.IsSelected = !app.IsSelected;
    }
}
