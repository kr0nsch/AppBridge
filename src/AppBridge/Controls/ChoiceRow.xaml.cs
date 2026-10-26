using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace AppBridge.Controls;

/// <summary>A row that navigates when clicked, used for the choices on the start screens.</summary>
public sealed partial class ChoiceRow : UserControl
{
    public static readonly DependencyProperty GlyphProperty = Register(nameof(Glyph), "");

    /// <summary>Same tones as <see cref="HeroBadge"/>.</summary>
    public static readonly DependencyProperty ToneProperty = Register(nameof(Tone), "Blue");

    public static readonly DependencyProperty TitleProperty = Register(nameof(Title), "");

    public static readonly DependencyProperty SubtitleProperty = Register(nameof(Subtitle), "");

    /// <summary>Shows the subtitle in the accent colour, for good news such as "21 apps saved".</summary>
    public static readonly DependencyProperty SubtitleIsAccentProperty = Register(nameof(SubtitleIsAccent), false);

    public static readonly DependencyProperty IsRowEnabledProperty = Register(nameof(IsRowEnabled), true);

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(ChoiceRow), new PropertyMetadata(null));

    public ChoiceRow()
    {
        InitializeComponent();
        Update();
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string Tone
    {
        get => (string)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public bool SubtitleIsAccent
    {
        get => (bool)GetValue(SubtitleIsAccentProperty);
        set => SetValue(SubtitleIsAccentProperty, value);
    }

    public bool IsRowEnabled
    {
        get => (bool)GetValue(IsRowEnabledProperty);
        set => SetValue(IsRowEnabledProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>Moves keyboard focus to the row.</summary>
    public void FocusRow() => RowButton.Focus(FocusState.Programmatic);

    private static DependencyProperty Register<T>(string name, T fallback) =>
        DependencyProperty.Register(name, typeof(T), typeof(ChoiceRow), new PropertyMetadata(fallback, (d, _) => ((ChoiceRow)d).Update()));

    private void Update()
    {
        if (Tile is null)
            return;
        Tile.Background = HeroBadge.ToneBrush(Tone);
        if (new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast)
            GlyphIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush((Windows.UI.Color)Application.Current.Resources["SystemColorHighlightTextColor"]);
        GlyphIcon.Glyph = Glyph ?? "";
        TitleText.Text = Title ?? "";
        SubtitleText.Text = AccentSubtitleText.Text = Subtitle ?? "";
        var hasSubtitle = !string.IsNullOrEmpty(Subtitle);
        SubtitleText.Visibility = hasSubtitle && !SubtitleIsAccent ? Visibility.Visible : Visibility.Collapsed;
        AccentSubtitleText.Visibility = hasSubtitle && SubtitleIsAccent ? Visibility.Visible : Visibility.Collapsed;
        RowButton.IsEnabled = IsRowEnabled;
        AutomationProperties.SetName(RowButton, string.IsNullOrEmpty(Subtitle) ? Title : $"{Title}, {Subtitle}");
    }

    private void OnClick(object sender, RoutedEventArgs e)
    {
        if (Command?.CanExecute(null) == true)
            Command.Execute(null);
    }
}
