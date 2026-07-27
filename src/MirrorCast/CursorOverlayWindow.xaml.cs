using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using MirrorCast.Interop;

namespace MirrorCast;

/// <summary>
/// Draws the synthetic mouse cursor over the mirrored picture.
///
/// This has to be its own top-level window: DWM composites a registered thumbnail
/// ON TOP of the destination window's own content, so anything drawn inside
/// <see cref="MirrorWindow"/> is buried underneath the mirrored image and never
/// visible. A separate HWND is composited independently and can sit above it.
///
/// The window is click-through (WS_EX_TRANSPARENT), never activates
/// (WS_EX_NOACTIVATE) and stays out of Alt-Tab (WS_EX_TOOLWINDOW), so it cannot
/// interfere with whatever the presenter is doing on the main screen.
/// </summary>
public partial class CursorOverlayWindow : Window
{
    private IntPtr _hwnd;
    private bool _placedAboveMirror;

    public CursorOverlayWindow()
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
    /// Positions and shows the cursor bitmap. All coordinates are physical pixels,
    /// matching how the mirror window itself is positioned, so this stays correct
    /// across monitors with different DPI scaling.
    /// </summary>
    public void ShowCursor(BitmapSource bitmap, int x, int y, int width, int height, IntPtr mirrorHwnd)
    {
        if (_hwnd == IntPtr.Zero) return;

        if (!ReferenceEquals(CursorImage.Source, bitmap))
            CursorImage.Source = bitmap;

        if (!_placedAboveMirror)
        {
            // Re-asserting topmost puts this window at the top of the topmost band,
            // i.e. above the mirror window (which is also topmost).
            User32.SetWindowPos(_hwnd, User32.HWND_TOPMOST, x, y, width, height,
                User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);
            _placedAboveMirror = true;
        }
        else
        {
            User32.SetWindowPos(_hwnd, IntPtr.Zero, x, y, width, height,
                User32.SWP_NOACTIVATE | User32.SWP_NOZORDER | User32.SWP_SHOWWINDOW);
        }
    }

    public void HideCursor()
    {
        if (_hwnd == IntPtr.Zero) return;

        User32.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
            User32.SWP_NOACTIVATE | User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOZORDER | User32.SWP_HIDEWINDOW);
        _placedAboveMirror = false;
    }
}
