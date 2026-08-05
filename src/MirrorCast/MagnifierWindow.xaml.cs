using System.Windows;
using System.Windows.Interop;
using MirrorCast.Interop;

namespace MirrorCast;

/// <summary>Hosts a second DWM thumbnail used as a pointer-centered magnifier.</summary>
public partial class MagnifierWindow : Window
{
    private IntPtr _hwnd;

    public IntPtr Handle => _hwnd;

    public MagnifierWindow()
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

    public void ShowAt(int x, int y, int size)
    {
        if (_hwnd == IntPtr.Zero) return;
        User32.SetWindowPos(_hwnd, User32.HWND_TOPMOST, x, y, size, size,
            User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);
    }

    public void HideMagnifier()
    {
        if (_hwnd == IntPtr.Zero) return;
        User32.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
            User32.SWP_NOACTIVATE | User32.SWP_NOMOVE | User32.SWP_NOSIZE |
            User32.SWP_NOZORDER | User32.SWP_HIDEWINDOW);
    }
}
