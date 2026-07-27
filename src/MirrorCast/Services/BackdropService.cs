using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MirrorCast.Interop;

namespace MirrorCast.Services;

/// <summary>
/// Applies a native DWM backdrop (Mica on Windows 11, acrylic blur-behind on Windows 10 1809+)
/// plus a matching dark/light immersive titlebar, so the window reads system theme changes live.
/// </summary>
public static class BackdropService
{
    public static void Apply(Window window, bool isDarkTheme)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        // WPF's own render surface paints an opaque black backdrop under a "Transparent"
        // Window.Background unless the HwndTarget itself is told the background is transparent.
        // Without this, DWM has nothing see-through to composite Mica/acrylic into.
        if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is HwndTarget hwndTarget)
        {
            hwndTarget.BackgroundColor = Colors.Transparent;
        }

        // DWM only extends the "glass sheet" behind the non-client titlebar by default.
        // Extending it with -1 margins pulls the backdrop into the entire client area too,
        // which is where our WPF content actually lives.
        var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmApi.DwmExtendFrameIntoClientArea(hwnd, ref margins);

        SetDarkTitleBar(hwnd, isDarkTheme);

        if (Environment.OSVersion.Version.Build >= 22000)
        {
            ApplyMica(hwnd, isDarkTheme);
        }
        else
        {
            ApplyAcrylicBlur(hwnd, isDarkTheme);
        }
    }

    private static void SetDarkTitleBar(IntPtr hwnd, bool isDark)
    {
        int value = isDark ? 1 : 0;
        DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    private static void ApplyMica(IntPtr hwnd, bool isDarkTheme)
    {
        int cornerPref = DwmApi.DWMWCP_ROUND;
        DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

        int backdropType = DwmApi.DWMSBT_MAINWINDOW;
        int hr = DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
        if (hr != 0)
        {
            // Pre-22621 builds don't support DWMWA_SYSTEMBACKDROP_TYPE; fall back to blur-behind.
            ApplyAcrylicBlur(hwnd, isDarkTheme);
        }
    }

    private static void ApplyAcrylicBlur(IntPtr hwnd, bool isDarkTheme)
    {
        int tint = isDarkTheme ? 0x00201F1F : 0x00F5F5F5; // 0x00BBGGRR
        var accent = new AccentPolicy
        {
            AccentState = CompositionApi.ACCENT_ENABLE_ACRYLICBLURBEHIND,
            AccentFlags = 2,
            GradientColor = (140 << 24) | tint,
            AnimationId = 0
        };

        int size = Marshal.SizeOf(accent);
        IntPtr accentPtr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = CompositionApi.WCA_ACCENT_POLICY,
                SizeOfData = size,
                Data = accentPtr
            };

            CompositionApi.SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }
}
