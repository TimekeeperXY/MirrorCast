using System.Runtime.InteropServices;
using MirrorCast.Interop;
using MirrorCast.Models;

namespace MirrorCast.Services;

public static class MonitorEnumerator
{
    public static List<MonitorInfo> EnumerateMonitors()
    {
        var monitors = new List<MonitorInfo>();
        int index = 0;

        User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (User32.GetMonitorInfo(hMonitor, ref mi))
            {
                index++;
                monitors.Add(new MonitorInfo
                {
                    Index = index,
                    DeviceName = mi.szDevice,
                    Bounds = mi.rcMonitor,
                    WorkArea = mi.rcWork,
                    IsPrimary = (mi.dwFlags & User32.MONITORINFOF_PRIMARY) != 0
                });
            }
            return true;
        }, IntPtr.Zero);

        return monitors.OrderByDescending(m => m.IsPrimary).ThenBy(m => m.Index).ToList();
    }
}
