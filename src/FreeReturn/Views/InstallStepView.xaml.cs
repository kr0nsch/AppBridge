using FreeReturn.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FreeReturn.Views;

public sealed partial class InstallStepView : UserControl
{
    public InstallStepView(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public MainViewModel ViewModel { get; }
}
