using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using MirrorCast.Interop;

namespace MirrorCast.Services;

public static class IconExtractor
{
    public static Icon? GetAppIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            return string.IsNullOrEmpty(path) ? null : Icon.ExtractAssociatedIcon(path);
        }
        catch
        {
            return null;
        }
    }

    public static BitmapSource? GetAppIconBitmapSource()
    {
        using var icon = GetAppIcon();
        if (icon == null) return null;

        var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        src.Freeze();
        return src;
    }

    public static BitmapSource? GetWindowIcon(IntPtr hwnd, string processName)
    {
        try
        {
            IntPtr hIcon = SendGetIcon(hwnd, User32.ICON_SMALL2);
            if (hIcon == IntPtr.Zero) hIcon = SendGetIcon(hwnd, User32.ICON_BIG);
            if (hIcon == IntPtr.Zero) hIcon = User32.GetClassLongPtr(hwnd, User32.GCL_HICONSM);
            if (hIcon == IntPtr.Zero) hIcon = User32.GetClassLongPtr(hwnd, User32.GCL_HICON);

            if (hIcon != IntPtr.Zero)
            {
                var src = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
        }
        catch
        {
            // fall through to exe-icon fallback
        }

        return GetIconFromProcessPath(processName);
    }

    private static IntPtr SendGetIcon(IntPtr hwnd, int type)
    {
        User32.SendMessageTimeout(hwnd, User32.WM_GETICON, (IntPtr)type, IntPtr.Zero, User32.SMTO_ABORTIFHUNG, 200, out var result);
        return result;
    }

    private static BitmapSource? GetIconFromProcessPath(string processName)
    {
        try
        {
            var name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName[..^4]
                : processName;

            using var process = System.Diagnostics.Process.GetProcessesByName(name).FirstOrDefault();
            var path = process?.MainModule?.FileName;
            if (string.IsNullOrEmpty(path)) return null;

            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon == null) return null;

            var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch
        {
            return null;
        }
    }
}
