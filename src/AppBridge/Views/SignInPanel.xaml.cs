using AppBridge.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace AppBridge.Views;

public sealed partial class SignInPanel : UserControl
{
    public static readonly DependencyProperty AccountProperty =
        DependencyProperty.Register(nameof(Account), typeof(AccountViewModel), typeof(SignInPanel), new PropertyMetadata(null));

    public SignInPanel()
    {
        InitializeComponent();
    }

    public AccountViewModel Account
    {
        get => (AccountViewModel)GetValue(AccountProperty);
        set => SetValue(AccountProperty, value);
    }

    private void OnFieldKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && Account.SignInCommand.CanExecute(null))
        {
            e.Handled = true;
            Account.SignInCommand.Execute(null);
        }
    }
}
