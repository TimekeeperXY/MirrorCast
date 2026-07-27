using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MirrorCast.Interop;
using Point = System.Windows.Point;
using Color = System.Drawing.Color;
using GdiPixelFormat = System.Drawing.Imaging.PixelFormat;

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
        if (info.hCursor == IntPtr.Zero) return false;

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
            if (!TryGetCursorSize(iconInfo, out int w, out int h)) return false;

            var rendered = RenderCursor(info.hCursor, w, h);
            if (rendered == null) return false;

            _cachedHCursor = info.hCursor;
            _cachedBitmap = rendered;
            _cachedHotspotX = iconInfo.xHotspot;
            _cachedHotspotY = iconInfo.yHotspot;

            bitmap = rendered;
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

    private static bool TryGetCursorSize(ICONINFO iconInfo, out int width, out int height)
    {
        width = height = 0;
        var bm = new BITMAP();
        int size = Marshal.SizeOf<BITMAP>();

        if (iconInfo.hbmColor != IntPtr.Zero && User32.GetObject(iconInfo.hbmColor, size, ref bm) != 0)
        {
            width = bm.bmWidth;
            height = bm.bmHeight;
        }
        else if (iconInfo.hbmMask != IntPtr.Zero && User32.GetObject(iconInfo.hbmMask, size, ref bm) != 0)
        {
            // A mask-only cursor stores the AND mask stacked above the XOR mask.
            width = bm.bmWidth;
            height = bm.bmHeight / 2;
        }

        return width > 0 && height > 0;
    }

    /// <summary>
    /// Renders a cursor to a straight-alpha bitmap by drawing it over black and over white
    /// and solving for per-pixel alpha.
    ///
    /// <see cref="Imaging.CreateBitmapSourceFromHIcon"/> cannot be used: inverting (XOR)
    /// cursors such as the standard I-beam come back completely transparent from it, because
    /// "invert whatever is behind me" has no representation in a static ARGB bitmap. Those
    /// pixels are detected here (they get *darker* over a white background) and drawn as a
    /// solid glyph instead, which is what the presenter actually sees on a normal text field.
    /// </summary>
    private static BitmapSource? RenderCursor(IntPtr hCursor, int width, int height)
    {
        using var overBlack = DrawOver(hCursor, width, height, Color.Black);
        using var overWhite = DrawOver(hCursor, width, height, Color.White);
        if (overBlack == null || overWhite == null) return null;

        var rect = new Rectangle(0, 0, width, height);
        var db = overBlack.LockBits(rect, ImageLockMode.ReadOnly, GdiPixelFormat.Format32bppArgb);
        var dw = overWhite.LockBits(rect, ImageLockMode.ReadOnly, GdiPixelFormat.Format32bppArgb);

        try
        {
            int count = width * height * 4;
            var black = new byte[count];
            var white = new byte[count];
            Marshal.Copy(db.Scan0, black, 0, count);
            Marshal.Copy(dw.Scan0, white, 0, count);

            var outPixels = new byte[count];
            bool anyVisible = false;

            for (int i = 0; i < count; i += 4)
            {
                // BGRA byte order.
                int bB = black[i], bG = black[i + 1], bR = black[i + 2];
                int wB = white[i], wG = white[i + 1], wR = white[i + 2];

                // Over white a partially transparent pixel is brighter than over black;
                // the gap is exactly the amount of background showing through.
                int gap = ((wR - bR) + (wG - bG) + (wB - bB)) / 3;
                int alpha = 255 - gap;

                if (alpha <= 0)
                {
                    // Fully transparent.
                    continue;
                }

                byte r, g, b;
                if (gap < 0)
                {
                    // Inverting pixel: it got darker over white. Nothing sensible to
                    // un-blend, so render an opaque dark glyph — text fields are
                    // overwhelmingly light, which is where an I-beam is normally seen.
                    alpha = 255;
                    r = g = b = 0;
                }
                else
                {
                    alpha = Math.Min(alpha, 255);
                    // Un-premultiply against the black pass.
                    r = (byte)Math.Clamp(bR * 255 / alpha, 0, 255);
                    g = (byte)Math.Clamp(bG * 255 / alpha, 0, 255);
                    b = (byte)Math.Clamp(bB * 255 / alpha, 0, 255);
                }

                outPixels[i] = b;
                outPixels[i + 1] = g;
                outPixels[i + 2] = r;
                outPixels[i + 3] = (byte)alpha;
                anyVisible = true;
            }

            if (!anyVisible) return null;

            AddContrastOutlineIfSingleTone(outPixels, width, height);

            var bmp = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, outPixels, width * 4);
            bmp.Freeze();
            return bmp;
        }
        finally
        {
            overBlack.UnlockBits(db);
            overWhite.UnlockBits(dw);
        }
    }

    /// <summary>
    /// Some system cursors — notably the Windows 11 I-beam — are a single flat tone with no
    /// built-in outline, because Windows gives them contrast when it composites the real
    /// pointer. A faithful copy of one is therefore invisible against a similarly coloured
    /// background (a white I-beam on a white text field). Cursors that already carry their own
    /// light-and-dark detail, like the standard arrow, are left untouched.
    /// </summary>
    private static void AddContrastOutlineIfSingleTone(byte[] pixels, int width, int height)
    {
        int minLum = 255, maxLum = 0;
        long sumLum = 0;
        int opaqueCount = 0;

        for (int i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 3] < 128) continue;
            int lum = (pixels[i] * 114 + pixels[i + 1] * 587 + pixels[i + 2] * 299) / 1000;
            minLum = Math.Min(minLum, lum);
            maxLum = Math.Max(maxLum, lum);
            sumLum += lum;
            opaqueCount++;
        }

        if (opaqueCount == 0) return;
        if (maxLum - minLum > 90) return; // already has its own contrast

        byte outline = (byte)(sumLum / opaqueCount >= 128 ? 0 : 255);

        // Ring the glyph with the opposite tone, writing only into fully transparent pixels
        // so the glyph itself is never altered.
        var original = (byte[])pixels.Clone();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = (y * width + x) * 4;
                if (original[idx + 3] != 0) continue;

                bool touchesGlyph = false;
                for (int dy = -1; dy <= 1 && !touchesGlyph; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        if (original[(ny * width + nx) * 4 + 3] >= 128) { touchesGlyph = true; break; }
                    }
                }

                if (!touchesGlyph) continue;

                pixels[idx] = outline;
                pixels[idx + 1] = outline;
                pixels[idx + 2] = outline;
                pixels[idx + 3] = 255;
            }
        }
    }

    private static Bitmap? DrawOver(IntPtr hCursor, int width, int height, Color background)
    {
        var bmp = new Bitmap(width, height, GdiPixelFormat.Format32bppArgb);
        try
        {
            using var g = Graphics.FromImage(bmp);
            g.Clear(background);

            IntPtr hdc = g.GetHdc();
            try
            {
                if (!User32.DrawIconEx(hdc, 0, 0, hCursor, width, height, 0, IntPtr.Zero, User32.DI_NORMAL))
                {
                    bmp.Dispose();
                    return null;
                }
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }

            return bmp;
        }
        catch
        {
            bmp.Dispose();
            return null;
        }
    }
}
