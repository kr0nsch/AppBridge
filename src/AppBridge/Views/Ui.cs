using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AppBridge.Views;

/// <summary>Small helpers for x:Bind function bindings.</summary>
public static class Ui
{
    public static Visibility Collapsed(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static double DimIf(bool value) => value ? 0.45 : 1.0;

    public static bool Not(bool value) => !value;

    private static readonly Brush ChosenTint = new SolidColorBrush(Color.FromArgb(0x1F, 0x00, 0x7A, 0xFF));
    private static readonly Brush NoTint = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

    /// <summary>Soft Apple-blue tint for the chosen row (works in light and dark).</summary>
    public static Brush TintIf(bool value) =>
        value && !new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast ? ChosenTint : NoTint;
}
