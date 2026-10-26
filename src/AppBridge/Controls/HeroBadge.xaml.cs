using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace AppBridge.Controls;

/// <summary>Hero artwork for each step: a gradient squircle with a white glyph.</summary>
public sealed partial class HeroBadge : UserControl
{
    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(HeroBadge), new PropertyMetadata("", (d, _) => ((HeroBadge)d).Update()));

    /// <summary>Blue, Teal, Indigo, Green or Orange.</summary>
    public static readonly DependencyProperty ToneProperty =
        DependencyProperty.Register(nameof(Tone), typeof(string), typeof(HeroBadge), new PropertyMetadata("Blue", (d, _) => ((HeroBadge)d).Update()));

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(HeroBadge), new PropertyMetadata(-1.0, (d, _) => ((HeroBadge)d).Update()));

    private static readonly Dictionary<string, (Color From, Color To)> Tones = new()
    {
        ["Blue"] = (Color.FromArgb(255, 0, 150, 220), Color.FromArgb(255, 60, 70, 230)),
        ["Teal"] = (Color.FromArgb(255, 48, 196, 200), Color.FromArgb(255, 0, 122, 255)),
        ["Indigo"] = (Color.FromArgb(255, 120, 110, 255), Color.FromArgb(255, 88, 60, 214)),
        ["Green"] = (Color.FromArgb(255, 72, 214, 120), Color.FromArgb(255, 20, 160, 110)),
        ["Orange"] = (Color.FromArgb(255, 255, 179, 64), Color.FromArgb(255, 255, 112, 40)),
    };

    public HeroBadge()
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

    /// <summary>0..100 shows a ring around the badge; negative hides it.</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>A short spring-like pop, used when a step finishes.</summary>
    public void Pop()
    {
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
            return;
        var story = new Storyboard();
        foreach (var property in new[] { "ScaleX", "ScaleY" })
        {
            var frames = new DoubleAnimationUsingKeyFrames();
            frames.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 0.7 });
            frames.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(260), Value = 1.08, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            frames.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(460), Value = 1.0, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
            Storyboard.SetTarget(frames, PopScale);
            Storyboard.SetTargetProperty(frames, property);
            story.Children.Add(frames);
        }

        story.Begin();
    }

    private void Update()
    {
        if (Tile is null)
            return;

        var (from, to) = Tones.TryGetValue(Tone ?? "", out var tone) ? tone : Tones["Blue"];
        if (new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast)
        {
            // Follow the user's high-contrast palette instead of brand colours.
            from = to = (Color)Application.Current.Resources["SystemColorHighlightColor"];
            GlyphIcon.Foreground = new SolidColorBrush((Color)Application.Current.Resources["SystemColorHighlightTextColor"]);
            Glow.Visibility = Visibility.Collapsed;
        }
        Tile.Background = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0.15, 0),
            EndPoint = new Windows.Foundation.Point(0.85, 1),
            GradientStops = { new GradientStop { Color = from, Offset = 0 }, new GradientStop { Color = to, Offset = 1 } },
        };
        Glow.Fill = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop { Color = Color.FromArgb(150, to.R, to.G, to.B), Offset = 0 },
                new GradientStop { Color = Color.FromArgb(0, to.R, to.G, to.B), Offset = 1 },
            },
        };
        GlyphIcon.Glyph = Glyph ?? "";

        var showRing = Progress >= 0;
        Ring.Visibility = showRing ? Visibility.Visible : Visibility.Collapsed;
        Ring.IsActive = showRing;
        Ring.Value = Math.Clamp(Progress, 0, 100);
        Ring.Foreground = new SolidColorBrush(to);
        Ring.Background = new SolidColorBrush(Color.FromArgb(40, to.R, to.G, to.B));
        Tile.Width = Tile.Height = showRing ? 66 : 76;
        Tile.CornerRadius = new CornerRadius(showRing ? 17 : 20);
        GlyphIcon.FontSize = showRing ? 30 : 34;
    }
}
