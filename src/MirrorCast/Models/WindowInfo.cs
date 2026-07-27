using System.Windows.Media.Imaging;

namespace MirrorCast.Models;

public class WindowInfo
{
    public IntPtr Hwnd { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public bool IsMinimized { get; set; }
    public BitmapSource? Icon { get; set; }

    public string DisplayTitle => IsMinimized ? $"{Title} (已最小化)" : Title;
}
