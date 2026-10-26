using Microsoft.UI.Xaml;

namespace FreeReturn.Views;

/// <summary>Small helpers for x:Bind function bindings.</summary>
public static class Ui
{
    public static Visibility Collapsed(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static double DimIf(bool value) => value ? 0.45 : 1.0;

    public static bool Not(bool value) => !value;
}
