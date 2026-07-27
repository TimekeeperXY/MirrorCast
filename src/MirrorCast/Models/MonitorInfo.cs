using MirrorCast.Interop;

namespace MirrorCast.Models;

public class MonitorInfo
{
    public int Index { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public RECT Bounds { get; set; }
    public RECT WorkArea { get; set; }
    public bool IsPrimary { get; set; }

    public string Resolution => $"{Bounds.Width}×{Bounds.Height}";
    public string DisplayName => IsPrimary ? $"显示器 {Index} (主)" : $"显示器 {Index}";
}
