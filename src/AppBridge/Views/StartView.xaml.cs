using AppBridge.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace AppBridge.Views;

/// <summary>The first screen: transfer now (both iPhones here) or later (save apps on this PC).</summary>
public sealed partial class StartView : UserControl
{
    public StartView(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public MainViewModel ViewModel { get; }
}
