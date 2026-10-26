using AppBridge.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace AppBridge.Views;

public sealed partial class InstallStepView : UserControl
{
    public InstallStepView(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ViewModel.Install.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InstallViewModel.State) && ViewModel.Install.IsFinished)
                Hero.Pop();
        };
    }

    public MainViewModel ViewModel { get; }
}
