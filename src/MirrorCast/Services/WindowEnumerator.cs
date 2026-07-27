using System.Diagnostics;
using System.Text;
using MirrorCast.Interop;
using MirrorCast.Models;

namespace MirrorCast.Services;

public static class WindowEnumerator
{
    private static readonly HashSet<string> ExcludedClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "Button", "tooltips_class32", "MultitaskingViewFrame", "Windows.UI.Core.CoreWindow"
    };

    public static List<WindowInfo> EnumerateMirrorableWindows(IntPtr selfHwnd)
    {
        var results = new List<WindowInfo>();
        var currentPid = (uint)Environment.ProcessId;

        User32.EnumWindows((hWnd, _) =>
        {
            if (hWnd == selfHwnd) return true;
            if (!User32.IsWindowVisible(hWnd)) return true;

            int len = User32.GetWindowTextLength(hWnd);
            if (len == 0) return true;

            var exStyle = User32.GetWindowLongPtr(hWnd, User32.GWL_EXSTYLE).ToInt64();
            if ((exStyle & User32.WS_EX_TOOLWINDOW) != 0) return true;

            var classSb = new StringBuilder(256);
            User32.GetClassName(hWnd, classSb, classSb.Capacity);
            if (ExcludedClasses.Contains(classSb.ToString())) return true;

            if (User32.GetWindow(hWnd, User32.GW_OWNER) != IntPtr.Zero) return true;

            // Skip cloaked windows (e.g. suspended/hidden UWP surfaces)
            if (DwmApi.DwmGetWindowAttribute(hWnd, DwmApi.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            User32.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == currentPid) return true;

            var titleSb = new StringBuilder(len + 1);
            User32.GetWindowText(hWnd, titleSb, titleSb.Capacity);
            var title = titleSb.ToString();
            if (string.IsNullOrWhiteSpace(title)) return true;

            string processName;
            try
            {
                using var process = Process.GetProcessById((int)pid);
                processName = process.ProcessName + ".exe";
            }
            catch
            {
                return true; // process already exited between enumeration and lookup
            }

            results.Add(new WindowInfo
            {
                Hwnd = hWnd,
                Title = title,
                ProcessName = processName,
                IsMinimized = User32.IsIconic(hWnd),
                Icon = IconExtractor.GetWindowIcon(hWnd, processName)
            });

            return true;
        }, IntPtr.Zero);

        return results;
    }
}
