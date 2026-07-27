using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using MirrorCast.Interop;
using Point = System.Windows.Point;

namespace MirrorCast.Services;

public static class CursorHelper
{
    private static IntPtr _cachedHCursor = IntPtr.Zero;
    private static BitmapSource? _cachedBitmap;
    private static double _cachedHotspotX;
    private static double _cachedHotspotY;

    public static bool TryGetCurrentCursor(out BitmapSource? bitmap, out double hotspotX, out double hotspotY, out Point screenPos)
    {
        bitmap = null;
        hotspotX = 0;
        hotspotY = 0;
        screenPos = default;

        var info = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!User32.GetCursorInfo(ref info)) return false;
        if ((info.flags & User32.CURSOR_SHOWING) == 0) return false;

        screenPos = new Point(info.ptScreenPos.X, info.ptScreenPos.Y);

        if (info.hCursor == _cachedHCursor && _cachedBitmap != null)
        {
            bitmap = _cachedBitmap;
            hotspotX = _cachedHotspotX;
            hotspotY = _cachedHotspotY;
            return true;
        }

        if (!User32.GetIconInfo(info.hCursor, out var iconInfo)) return false;

        try
        {
            var src = Imaging.CreateBitmapSourceFromHIcon(info.hCursor, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();

            _cachedHCursor = info.hCursor;
            _cachedBitmap = src;
            _cachedHotspotX = iconInfo.xHotspot;
            _cachedHotspotY = iconInfo.yHotspot;

            bitmap = src;
            hotspotX = iconInfo.xHotspot;
            hotspotY = iconInfo.yHotspot;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (iconInfo.hbmColor != IntPtr.Zero) User32.DeleteObject(iconInfo.hbmColor);
            if (iconInfo.hbmMask != IntPtr.Zero) User32.DeleteObject(iconInfo.hbmMask);
        }
    }
}
