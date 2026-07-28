using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MirrorCast.Interop;
using Color = System.Windows.Media.Color;

namespace MirrorCast;

/// <summary>
/// Draws the expanding "click here" ripple over the mirrored picture.
///
/// Like <see cref="CursorOverlayWindow"/> this has to be its own top-level window: DWM
/// composites a registered thumbnail ON TOP of the destination window's own content, so
/// anything drawn inside <see cref="MirrorWindow"/> is buried under the mirrored image.
///
/// Click-through (WS_EX_TRANSPARENT), never activates (WS_EX_NOACTIVATE) and stays out of
/// Alt-Tab (WS_EX_TOOLWINDOW), so a ripple can never swallow the presenter's next click.
/// </summary>
public partial class ClickRippleWindow : Window
{
    /// Diameter of the ripple at full expansion, in physical pixels before DPI scaling.
    public const int RippleSizePx = 160;

    private static readonly Color LeftColor = Color.FromRgb(0xFF, 0x3B, 0x30);   // red
    private static readonly Color RightColor = Color.FromRgb(0x0A, 0x84, 0xFF);  // blue

    private IntPtr _hwnd;
    private Storyboard? _storyboard;

    public ClickRippleWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        long exStyle = User32.GetWindowLongPtr(_hwnd, User32.GWL_EXSTYLE).ToInt64();
        exStyle |= User32.WS_EX_TRANSPARENT | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW;
        User32.SetWindowLongPtr(_hwnd, User32.GWL_EXSTYLE, new IntPtr(exStyle));
    }

    /// <summary>
    /// Plays one ripple centred on the given screen point. Coordinates are physical pixels,
    /// matching how the mirror window itself is positioned, so this stays correct across
    /// monitors with different DPI scaling.
    /// </summary>
    public void Play(int centreX, int centreY, int size, bool isRightButton)
    {
        if (_hwnd == IntPtr.Zero) return;

        var colour = isRightButton ? RightColor : LeftColor;
        Ripple.Stroke = new SolidColorBrush(colour);
        Ripple.Fill = new SolidColorBrush(Color.FromArgb(0x33, colour.R, colour.G, colour.B));

        // Re-asserting topmost puts this above the mirror window, which is also topmost.
        User32.SetWindowPos(_hwnd, User32.HWND_TOPMOST,
            centreX - size / 2, centreY - size / 2, size, size,
            User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);

        _storyboard?.Stop(this);

        var duration = TimeSpan.FromMilliseconds(450);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var grow = new DoubleAnimation(0.35, 1.0, duration) { EasingFunction = ease };
        var growY = new DoubleAnimation(0.35, 1.0, duration) { EasingFunction = ease };
        // Punch in to full opacity fast, then fade — reads as a "tap" rather than a pulse.
        var fade = new DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(duration)));

        Storyboard.SetTarget(grow, Ripple);
        Storyboard.SetTargetProperty(grow,
            new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
        Storyboard.SetTarget(growY, Ripple);
        Storyboard.SetTargetProperty(growY,
            new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
        Storyboard.SetTarget(fade, Ripple);
        Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));

        var storyboard = new Storyboard();
        storyboard.Children.Add(grow);
        storyboard.Children.Add(growY);
        storyboard.Children.Add(fade);
        storyboard.Completed += (_, _) => Hide();

        _storyboard = storyboard;
        storyboard.Begin(this, true);
    }

    public new void Hide()
    {
        if (_hwnd == IntPtr.Zero) return;

        User32.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
            User32.SWP_NOACTIVATE | User32.SWP_NOMOVE | User32.SWP_NOSIZE
            | User32.SWP_NOZORDER | User32.SWP_HIDEWINDOW);
    }
}
